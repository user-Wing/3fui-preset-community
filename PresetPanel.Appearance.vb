Imports System
Imports System.Reflection
Imports System.Drawing
Imports System.Windows.Forms
Imports LakeUI
Namespace PresetCommunity
    Public Partial Class PresetPanel
        Private Shared ReadOnly UiCanvas As Color = Color.FromArgb(24, 24, 24)
        Private Shared ReadOnly UiSurface As Color = Color.FromArgb(40, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceRaised As Color = Color.FromArgb(40, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceHover As Color = Color.FromArgb(60, 220, 220, 220)
        Private Shared ReadOnly UiSurfaceDark As Color = Color.FromArgb(40, 0, 0, 0)
        Private Shared ReadOnly UiSeparator As Color = Color.FromArgb(80, 220, 220, 220)
        Private Shared ReadOnly UiScrollThumb As Color = Color.FromArgb(80, 220, 220, 220)
        Private Shared ReadOnly UiScrollThumbHover As Color = Color.FromArgb(120, 220, 220, 220)
        Private Shared ReadOnly UiStrokeSoft As Color = Color.Transparent
        Private Shared ReadOnly UiAccent As Color = Color.FromArgb(71, 156, 255)
        Private Shared ReadOnly UiAccentHover As Color = Color.FromArgb(110, 71, 156, 255)
        Private Shared ReadOnly UiAccentPressed As Color = Color.FromArgb(140, 71, 156, 255)
        Private Shared ReadOnly UiSuccess As Color = Color.FromArgb(63, 205, 135)
        Private Shared ReadOnly UiDanger As Color = Color.FromArgb(235, 93, 93)
        Private Shared ReadOnly UiText As Color = Color.FromArgb(220, 220, 220)
        Private Shared ReadOnly UiTextSecondary As Color = Color.FromArgb(176, 220, 220, 220)
        Private Shared ReadOnly UiTextMuted As Color = Color.FromArgb(120, 255, 255, 255)

        ' 统一的 96 DPI 紧凑尺寸；字体保持不变，由布局容器负责换算实际 DPI。
        Private Const UiControlHeight As Integer = 28
        Private Const UiFieldHeight As Integer = 56
        Private Const UiFieldCaptionHeight As Integer = 20
        Private Const UiFieldEditorTop As Integer = 23
        Private Const UiRowHeight As Integer = 36
        Private Const UiColumnGap As Integer = 8
        Private Const UiCornerRadius As Integer = 6

        Private Shared Sub ConfigureDpiListColumns(list As UltraDetailListView, minimumFirstWidth As Integer)
            Dim lastScale As Double = 1.0R
            Dim arrange As Action =
                Sub()
                    If list.IsDisposed OrElse list.Columns.Count = 0 Then Return
                    Dim scale = If(list.IsHandleCreated, list.DeviceDpi / 96.0R, 1.0R)
                    list.BeginUpdate()
                    Try
                        Dim fixedWidth = 0
                        For index = 1 To list.Columns.Count - 1
                            ' 保留用户拖动后的宽度，只在 DPI 改变时按比例换算。
                            If Math.Abs(scale - lastScale) > 0.001R Then
                                list.Columns(index).Width = CInt(Math.Round(list.Columns(index).Width * scale / lastScale))
                            End If
                            fixedWidth += list.Columns(index).Width
                        Next
                        lastScale = scale
                        list.Columns(0).Width = Math.Max(CInt(Math.Round(minimumFirstWidth * scale)),
                            list.ClientSize.Width - list.Padding.Horizontal - CInt(Math.Round(18 * scale)) - fixedWidth)
                    Finally
                        list.EndUpdate()
                    End Try
                End Sub
            AddHandler list.HandleCreated, Sub(sender, e) arrange()
            AddHandler list.ClientSizeChanged, Sub(sender, e) arrange()
            AddHandler list.DpiChangedAfterParent, Sub(sender, e) arrange()
            arrange()
        End Sub

        ''' <summary>对齐 3FUI 准备文件页的半透明列表；保留各页面的列、行高和交互配置。</summary>
        Private Sub ConfigureTransparentListAppearance(list As UltraDetailListView)
            list.BackColor = Color.Transparent
            list.BackgroundColor = UiSurface
            list.BackgroundSource = ModernPanel1
            list.BorderColor = Color.Transparent
            list.BorderSize = 0
            list.BorderRadius = 10
            list.HeaderBackColor = Color.Transparent
            list.HeaderForeColor = UiTextSecondary
            list.HeaderBorderColor = UiSurface
            list.HeaderBorderWidth = 2
            list.ItemForeColor = UiTextSecondary
            list.ItemHoverBackColor = UiSurfaceHover
            list.ItemSelectedBackColor = UiSurface
            list.ItemCornerRadius = 10
            ' 准备文件页不使用分组；下载页的分组也不能沿用 LakeUI 的不透明默认值。
            list.GroupBackColor = UiSurfaceDark
            list.GroupForeColor = UiText
            list.GroupBorderColor = UiSurface
            list.ScrollBarWidth = 10
            list.ScrollBarTrackColor = UiSurface
            list.ScrollBarThumbColor = UiScrollThumb
            list.ScrollBarThumbHoverColor = UiScrollThumbHover
        End Sub


        Private Shared Sub ConfigureSecondaryButton(button As ModernButton)
            button.Font = New Font("Microsoft YaHei UI", 10.0F, FontStyle.Regular)
            button.ForeColor = UiText
            button.BorderRadius = UiCornerRadius
            button.BorderSize = 0
            button.BorderColor = Color.Transparent
            button.HoverBorderColor = Color.Transparent
            button.PressedBorderColor = Color.Transparent
            button.BackColor1 = UiSurfaceRaised
            button.BackColor2 = UiSurfaceRaised
            button.HoverBackColor1 = UiSurfaceHover
            button.HoverBackColor2 = UiSurfaceHover
            button.PressedBackColor1 = Color.FromArgb(80, 220, 220, 220)
            button.PressedBackColor2 = Color.FromArgb(80, 220, 220, 220)
        End Sub


        Private Shared Sub BindScrollableGpuBackgroundSources(root As Control, source As Control)
            If root Is Nothing OrElse source Is Nothing Then Return

            Dim provider = TryCast(root, D3D_IBackgroundSourceProvider)
            If provider IsNot Nothing Then
                Dim currentSource As Control = Nothing
                If Not provider.TryGetBackgroundSource(currentSource) OrElse currentSource Is Nothing Then
                    Dim sourceProperty = root.GetType().GetProperty(
                        "BackgroundSource", BindingFlags.Instance Or BindingFlags.Public)
                    If sourceProperty IsNot Nothing AndAlso sourceProperty.CanWrite AndAlso
                       sourceProperty.PropertyType.IsAssignableFrom(source.GetType()) Then
                        sourceProperty.SetValue(root, source)
                    End If
                End If
            End If

            For Each child As Control In root.Controls
                BindScrollableGpuBackgroundSources(child, source)
            Next
        End Sub


    End Class
End Namespace
