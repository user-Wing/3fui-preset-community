Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports LakeUI

Namespace PresetCommunity
    Public Partial Class PresetPanel
        Inherits UserControl

        ' 与模型下载页使用相同背景源名称，供宿主绑定背景。
        Private ReadOnly ModernPanel1 As New ModernPanel With {.Name = "ModernPanel1", .Dock = DockStyle.Fill, .BackColor = UiCanvas, .BackColor1 = UiCanvas, .BorderSize = 0}
        Private ReadOnly _tabs As New ModernTabControl()
        Private ReadOnly _pageDownloader As New ModernPanel()
        Private ReadOnly _list As New UltraDetailListView()
        Private ReadOnly _refresh As New ModernButton With {.Text = "刷新资源"}
        Private ReadOnly _all As New ModernButton With {.Text = "下载全部"}
        Private ReadOnly _status As New HtmlColorLabel With {.Dock = DockStyle.Fill, .AutoSize = False, .ForeColor = UiTextMuted, .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft}
        Private ReadOnly _lifetime As New CancellationTokenSource()
        Private ReadOnly _coordinator As New ModelDownloadCoordinator()
        Private ReadOnly _cancellations As New Dictionary(Of String, CancellationTokenSource)(StringComparer.OrdinalIgnoreCase)
        Private ReadOnly _rows As New Dictionary(Of String, UltraDetailListView.ListItem)(StringComparer.OrdinalIgnoreCase)
        Private _files As New List(Of PresetFile)
        Private _loading As Boolean
        Private _loaded As Boolean
        Private _batchBusy As Boolean
        Private _stop As Boolean
        Private _batchRow As UltraDetailListView.ListItem
        Private _menu As ModernContextMenu
        Private ReadOnly _directory As String = Path.Combine(Application.StartupPath, "Preset_v6", "Community")

        Public Sub New()
            SuspendLayout()
            AutoScaleMode = AutoScaleMode.None
            Try
                InitializeUi()
            Finally
                ' 与 VideoEnhancer 一致：全部控件先按 96 DPI 构建，再统一缩放。
                AutoScaleMode = AutoScaleMode.Dpi
                AutoScaleDimensions = New SizeF(96, 96)
                ResumeLayout(True)
            End Try
            AddHandler _refresh.Click, Async Sub(sender, e) Await LoadPresetsAsync()
            AddHandler _all.Click, Async Sub(sender, e)
                                       If _batchBusy Then
                                           _stop = True
                                           For Each cancellation In _cancellations.Values.ToArray()
                                               cancellation.Cancel()
                                           Next
                                           _all.Text = "正在停止"
                                       Else
                                           Await DownloadBatchAsync()
                                       End If
                                   End Sub
            AddHandler VisibleChanged, Async Sub(sender, e)
                                          If Visible AndAlso IsHandleCreated AndAlso Not _loaded Then Await LoadPresetsAsync()
                                      End Sub
            AddHandler HandleCreated, Async Sub(sender, e)
                                         If Visible AndAlso Not _loaded Then Await LoadPresetsAsync()
                                     End Sub
        End Sub

        Private Sub InitializeUi()
            Dock = DockStyle.Fill
            BackColor = UiCanvas
            MinimumSize = New Size(800, 520)
            Font = New Font("Microsoft YaHei UI", 10.0F)
            Padding = Padding.Empty
            ModernPanel1.Margin = Padding.Empty
            ModernPanel1.Padding = New Padding(20, 16, 20, 12)
            ModernPanel1.BackColor = Color.Transparent
            ModernPanel1.BackColor1 = Color.Transparent
            ModernPanel1.BorderRadius = 0
            Dim root As New ModernGridPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .BackColor = Color.Transparent, .Margin = Padding.Empty, .Padding = Padding.Empty}
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, 44.0F))
            _tabs.SuspendLayout()
            Try
                ConfigureTabs()
                BuildDownloadPage()
                _tabs.Items.Add(New ModernTabControl.ModernTab("预设社区") With {.BoundControl = _pageDownloader})
                _tabs.SelectedIndex = 0
            Finally
                _tabs.ResumeLayout(False)
            End Try
            root.AddAt(_tabs, 0, 0)
            Dim sectionStatus As New ModernGridPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 1, .BackColor = Color.Transparent, .Margin = Padding.Empty, .Padding = New Padding(0, 4, 0, 4)}
            sectionStatus.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
            sectionStatus.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            _status.Margin = Padding.Empty
            _status.BackColor = Color.Transparent
            _status.BackColor1 = Color.Transparent
            _status.BorderSize = 0
            _status.Text = "下载后在参数面板的预设管理中选择「从社区下载」"
            sectionStatus.AddAt(_status, 0, 0)
            root.AddAt(sectionStatus, 0, 1)
            ModernPanel1.Controls.Add(root)
            Controls.Add(ModernPanel1)
            BindScrollableGpuBackgroundSources(root, ModernPanel1)
        End Sub

        Private Sub ConfigureTabs()
            _tabs.Dock = DockStyle.Fill
            _tabs.ContentBackColor = Color.Transparent
            _tabs.BackColor = Color.Transparent
            _tabs.TabStripBackColor = Color.Transparent
            _tabs.TabStripOverlayColor = Color.Transparent
            _tabs.TabStripHeight = UiRowHeight
            _tabs.TabStripPadding = New Padding(0, 1, 0, 2)
            _tabs.TabItemTextPadding = 7
            _tabs.TabItemSpacing = 4
            _tabs.TabItemBorderRadius = UiCornerRadius
            _tabs.TabItemForeColor = UiTextMuted
            _tabs.TabItemSelectedForeColor = UiText
            _tabs.TabItemSelectedBackColor = UiSurface
            _tabs.TabItemHoverBackColor = UiSurfaceHover
            _tabs.IndicatorColor = UiAccent
            _tabs.IndicatorHeight = 2
            _tabs.IndicatorBorderRadius = 1
            _tabs.IndicatorPadding = 12
            _tabs.SeparatorWidth = 0
            _tabs.ContentBorderWidth = 0
            _tabs.TabAlignment = ModernTabControl.TabAlignmentEnum.Left
            _tabs.Font = New Font("Microsoft YaHei UI", 10.0F)
            _tabs.AnimationDuration = 0
            _tabs.AnimationFPS = 30
        End Sub

        Private Sub BuildDownloadPage()
            _pageDownloader.Dock = DockStyle.Fill
            _pageDownloader.BackColor = Color.Transparent
            _pageDownloader.BackColor1 = Color.Transparent
            _pageDownloader.BorderColor = Color.Transparent
            _pageDownloader.BorderSize = 0
            _pageDownloader.BorderRadius = 0
            _pageDownloader.BackgroundSource = ModernPanel1
            _pageDownloader.Padding = New Padding(0, 4, 0, 0)
            Dim root As New ModernGridPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2, .BackColor = Color.Transparent, .Margin = Padding.Empty, .Padding = Padding.Empty}
            root.RowStyles.Add(New RowStyle(SizeType.Absolute, UiRowHeight))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            Dim header As New ModernGridPanel With {.Dock = DockStyle.Fill, .ColumnCount = 3, .RowCount = 1, .BackColor = Color.Transparent, .Margin = Padding.Empty, .Padding = Padding.Empty}
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 128))
            header.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 136))
            header.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
            header.AddAt(New HtmlColorLabel With {.Dock = DockStyle.Fill, .AutoSize = False, .Margin = Padding.Empty, .Padding = Padding.Empty, .BackColor = Color.Transparent, .BackColor1 = Color.Transparent, .BorderSize = 0,
                .ForeColor = UiTextMuted, .TextAlign = HtmlColorLabel.TextAlignEnum.MiddleLeft,
                .Text = "<span style=""font-size:13; color:Silver"">预设资源库</span>   从 ModelScope 获取社区预设"}, 0, 0)
            For Each button In {_refresh, _all}
                button.Dock = DockStyle.Fill
                button.AutoSize = False
                button.Margin = New Padding(UiColumnGap, 4, 0, 4)
                ConfigureSecondaryButton(button)
            Next
            header.AddAt(_refresh, 1, 0)
            header.AddAt(_all, 2, 0)
            root.AddAt(header, 0, 0)
            ConfigureDownloadList()
            root.AddAt(_list, 0, 1)
            _pageDownloader.Controls.Add(root)
        End Sub

        Private Sub ConfigureDownloadList()
            _list.Dock = DockStyle.Fill
            _list.Margin = Padding.Empty
            _list.AutoScroll = False
            _list.Font = New Font("Microsoft YaHei UI", 9.2F)
            ConfigureTransparentListAppearance(_list)
            _list.HeaderVisible = True
            _list.HeaderHeight = 30
            _list.AllowColumnResize = True
            _list.MultiSelect = False
            _list.AllowDragReorder = False
            _list.ItemPadding = New Padding(10, 5, 8, 5)
            _list.ItemSpacing = 2
            _list.ContentPadding = New Padding(0, 4, 0, 4)
            _list.GroupHeight = 38
            _list.Columns.AddRange(New UltraDetailListView.ListColumn() {
                New UltraDetailListView.ListColumn("资源名称", 520), New UltraDetailListView.ListColumn("大小", 110),
                New UltraDetailListView.ListColumn("状态", 130), New UltraDetailListView.ListColumn("操作", 138)})
            ConfigureDpiListColumns(_list, 260)
            AddHandler _list.ItemClick, AddressOf OnItemClick
            AddHandler _list.MouseDown, AddressOf OnListMouseDown
        End Sub

        Private Async Function LoadPresetsAsync() As Task
            If _loading OrElse _batchBusy OrElse _coordinator.ActiveCount > 0 OrElse IsDisposed Then Return
            _loading = True
            UpdateButtons()
            Try
                _status.Text = "正在同步预设资源..."
                _files = Await PresetRepository.ListAsync(_lifetime.Token)
                If IsDisposed Then Return
                RenderFiles()
                _loaded = True
                _status.Text = $"已同步 {_files.Count} 个预设；下载后在预设管理中选择「从社区下载」"
            Catch ex As OperationCanceledException When _lifetime.IsCancellationRequested
            Catch ex As Exception
                _status.Text = "同步失败：" & System.Net.WebUtility.HtmlEncode(ex.Message) & "；点击刷新资源重试"
            Finally
                _loading = False
                If Not IsDisposed Then UpdateButtons()
            End Try
        End Function

        Private Sub RenderFiles()
            _list.BeginUpdate()
            Try
                _list.Items.Clear()
                _list.Groups.Clear()
                _rows.Clear()
                _list.Groups.Add(New UltraDetailListView.ListGroup("Preset", $"社区预设  ·  {_files.Count} 个文件") With {.ForeColor = UiText})
                _batchRow = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                    New UltraDetailListView.ListSubItem("本组资源", New Font("Microsoft YaHei UI", 9.2F, FontStyle.Bold), UiText),
                    New UltraDetailListView.ListSubItem(_files.Count & " 个文件"), New UltraDetailListView.ListSubItem(""), New UltraDetailListView.ListSubItem("")}) With {.GroupName = "Preset"}
                _list.Items.Add(_batchRow)
                For Each file In _files
                    Dim row = New UltraDetailListView.ListItem(New UltraDetailListView.ListSubItem() {
                        New UltraDetailListView.ListSubItem(Path.GetFileNameWithoutExtension(file.Name)),
                        New UltraDetailListView.ListSubItem((file.Size / 1024.0).ToString("0.0") & " KB"),
                        New UltraDetailListView.ListSubItem(""), New UltraDetailListView.ListSubItem("")}) With {.GroupName = "Preset", .Tag = file}
                    _list.Items.Add(row)
                    _rows(file.RelativePath) = row
                    RefreshRow(file)
                Next
                RefreshGroup()
            Finally
                _list.EndUpdate()
            End Try
        End Sub

        Private Sub SetRow(file As PresetFile, state As String, action As String, color As Color)
            If IsDisposed Then Return
            Dim row = _rows(file.RelativePath)
            row.SubItems(2).Text = state
            row.SubItems(2).ForeColor = color
            row.SubItems(3).Text = action
            row.SubItems(3).ForeColor = If(action = "已存在", UiTextMuted, UiAccent)
            _list.Invalidate()
        End Sub

        Private Sub RefreshRow(file As PresetFile)
            Dim current = PresetRepository.IsCurrent(file, _directory)
            Dim exists = IO.File.Exists(Path.Combine(_directory, file.Name))
            SetRow(file, If(current, "本地已安装", If(exists, "可更新", "未安装")), If(current, "已存在", If(exists, "更新", "下载")), If(current, UiSuccess, UiTextMuted))
        End Sub

        Private Sub RefreshGroup()
            If _batchRow Is Nothing Then Return
            Dim count = _files.Where(Function(file) PresetRepository.IsCurrent(file, _directory)).Count()
            _batchRow.SubItems(2).Text = $"{count}/{_files.Count} 已存在"
            _batchRow.SubItems(3).Text = If(count = _files.Count, "已全部存在", "下载本组")
            _batchRow.SubItems(3).ForeColor = If(count = _files.Count, UiTextMuted, UiAccent)
            _list.Invalidate()
        End Sub

        Private Async Sub OnItemClick(sender As Object, e As UltraDetailListView.ListItemEventArgs)
            If e.ColumnIndex <> 3 OrElse e.Item Is Nothing OrElse _loading Then Return
            Dim file = TryCast(e.Item.Tag, PresetFile)
            If file Is Nothing Then
                If Not _batchBusy Then Await DownloadBatchAsync()
                Return
            End If
            Dim cancellation As CancellationTokenSource = Nothing
            If _cancellations.TryGetValue(file.RelativePath, cancellation) Then
                cancellation.Cancel()
                SetRow(file, "正在取消", "请稍候...", UiTextMuted)
                Return
            End If
            If PresetRepository.IsCurrent(file, _directory) Then Return
            Dim queued = _coordinator.Enqueue("Preset", file.RelativePath)
            If queued <> ModelDownloadCoordinator.EnqueueResult.NoQueue Then Return
            If Not _coordinator.TryBegin(file.RelativePath) Then Return
            UpdateButtons()
            Try
                Await DownloadAsync(file)
            Finally
                _coordinator.EndPath(file.RelativePath)
                If Not IsDisposed Then
                    RefreshGroup()
                    UpdateButtons()
                End If
            End Try
        End Sub

        Private Async Function DownloadAsync(file As PresetFile) As Task(Of ModelDownloadCoordinator.ItemOutcome)
            Using cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token)
                _cancellations.Add(file.RelativePath, cancellation)
                Try
                    SetRow(file, "正在下载", "取消", UiAccent)
                    Dim progress As New Progress(Of Integer)(Sub(percent) SetRow(file, percent & "%", "取消", UiAccent))
                    Await PresetRepository.InstallAsync(file, _directory, progress, cancellation.Token)
                    If Not IsDisposed Then
                        RefreshRow(file)
                        RefreshHostPresets()
                    End If
                    Return ModelDownloadCoordinator.ItemOutcome.Succeeded
                Catch ex As OperationCanceledException
                    SetRow(file, "已取消", "重试", UiTextMuted)
                    Return ModelDownloadCoordinator.ItemOutcome.Cancelled
                Catch ex As HttpRequestException
                    SetRow(file, "下载失败", "重试", UiDanger)
                    If Not IsDisposed Then _status.Text = "下载失败：" & System.Net.WebUtility.HtmlEncode(ex.Message)
                    If ex.StatusCode = Net.HttpStatusCode.Unauthorized OrElse ex.StatusCode = Net.HttpStatusCode.Forbidden Then Return ModelDownloadCoordinator.ItemOutcome.AuthenticationRequired
                    If Not ex.StatusCode.HasValue Then Return ModelDownloadCoordinator.ItemOutcome.Offline
                    Return ModelDownloadCoordinator.ItemOutcome.Failed
                Catch ex As Exception
                    SetRow(file, "下载失败", "重试", UiDanger)
                    If Not IsDisposed Then _status.Text = "下载失败：" & System.Net.WebUtility.HtmlEncode(ex.Message)
                    Return ModelDownloadCoordinator.ItemOutcome.Failed
                Finally
                    _cancellations.Remove(file.RelativePath)
                End Try
            End Using
        End Function

        Private Async Function DownloadBatchAsync() As Task
            If _loading OrElse _batchBusy OrElse _coordinator.ActiveCount > 0 OrElse Not _loaded Then Return
            Dim files = _files.Where(Function(file) Not PresetRepository.IsCurrent(file, _directory)).ToDictionary(Function(file) file.RelativePath)
            If files.Count = 0 Then Return
            _batchBusy = True
            _stop = False
            _all.Text = "停止下载"
            UpdateButtons()
            For Each file In files.Values
                SetRow(file, "待下载", "排队中", UiTextMuted)
            Next
            Try
                Dim result = Await _coordinator.RunGroupAsync("Preset", files.Keys.ToList(),
                    Function(path) DownloadAsync(_files.First(Function(file) file.RelativePath = path)), Function(outcome) outcome,
                    Sub(path, outcome) RefreshGroup(),
                    Sub(batch)
                        If Not IsDisposed Then _status.Text = batch.Summary
                    End Sub, Function() _stop OrElse _lifetime.IsCancellationRequested)
                If Not IsDisposed Then
                    For Each file In _files
                        If _rows(file.RelativePath).SubItems(2).Text = "待下载" Then RefreshRow(file)
                    Next
                    _status.Text = result.Summary & If(result.StopReason = ModelDownloadCoordinator.QueueStopReason.None, "；下载完成", "；队列已停止，可重试")
                End If
            Finally
                _batchBusy = False
                If Not IsDisposed Then
                    _all.Text = "下载全部"
                    RefreshGroup()
                    UpdateButtons()
                End If
            End Try
        End Function

        Private Sub UpdateButtons()
            _refresh.Enabled = Not _loading AndAlso Not _batchBusy AndAlso _coordinator.ActiveCount = 0
            _all.Enabled = Not _loading AndAlso (_batchBusy OrElse (_loaded AndAlso _coordinator.ActiveCount = 0))
        End Sub

        Private Shared Sub RefreshHostPresets()
            ' 宿主已打开的社区列表同步刷新；未打开时宿主自行从目录读取。
            For Each form As Form In Application.OpenForms
                If form.GetType().Name = "Form_v6_参数面板_预设管理" Then form.GetType().GetMethod("刷新预设列表")?.Invoke(form, Nothing)
            Next
        End Sub

        Private Sub OnListMouseDown(sender As Object, e As MouseEventArgs)
            If e.Button <> MouseButtons.Right OrElse _loading OrElse _batchBusy OrElse _coordinator.ActiveCount > 0 Then Return
            Dim file = TryCast(_list.GetItemAt(e.X, e.Y)?.Tag, PresetFile)
            If file Is Nothing OrElse Not IO.File.Exists(Path.Combine(_directory, file.Name)) Then Return
            _menu?.Dispose()
            _menu = New ModernContextMenu()
            Dim deleteItem As New ModernContextMenu.ModernMenuItem("删除社区预设") With {.CloseOnClick = True, .ForeColor = UiDanger}
            AddHandler deleteItem.Click, Sub()
                Dim buttons As New List(Of ExMsgBoxModule.ExMsgBoxButton) From {New ExMsgBoxModule.ExMsgBoxButton("否", False), New ExMsgBoxModule.ExMsgBoxButton("是", True)}
                If ExMsgBoxModule.ExMsgBox("删除社区预设：" & file.Name & "？", buttons, "预设社区", 0, FindForm()) <> 1 Then Return
                Try
                    IO.File.Delete(Path.Combine(_directory, file.Name))
                    RefreshRow(file)
                    RefreshGroup()
                    RefreshHostPresets()
                Catch ex As Exception
                    _status.Text = System.Net.WebUtility.HtmlEncode(ex.Message)
                End Try
            End Sub
            _menu.Items.Add(deleteItem)
            _menu.Show(_list, e.Location)
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                _lifetime.Cancel()
                _menu?.Dispose()
            End If
            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
