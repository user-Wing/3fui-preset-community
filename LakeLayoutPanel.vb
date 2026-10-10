Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms
Imports LakeUI
Namespace PresetCommunity
    Friend Class DpiLayoutPanel
        Inherits ModernPanel

        Private _layoutScale As New SizeF(1.0F, 1.0F)

        Friend ReadOnly Property LayoutScale As SizeF
            Get
                Return _layoutScale
            End Get
        End Property

        Friend Function ScaleX(value As Integer) As Integer
            Return CInt(Math.Round(value * CDbl(LayoutScale.Width)))
        End Function

        Friend Function ScaleY(value As Integer) As Integer
            Return CInt(Math.Round(value * CDbl(LayoutScale.Height)))
        End Function

        Protected Overrides Sub ScaleControl(factor As SizeF, specified As BoundsSpecified)
            ' 必须在基类触发布局之前更新比例；窗口拉伸不会调用 DPI 缩放。
            ' WinForms 可能分开缩放位置与尺寸，只累计尺寸轴，避免同一轮缩放被计算两次。
            _layoutScale = New SizeF(
                LayoutScale.Width * If((specified And BoundsSpecified.Width) <> 0, factor.Width, 1.0F),
                LayoutScale.Height * If((specified And BoundsSpecified.Height) <> 0, factor.Height, 1.0F))
            MyBase.ScaleControl(factor, specified)
        End Sub
    End Class

    Friend Class ModernGridPanel
        Inherits DpiLayoutPanel

        Private NotInheritable Class Placement
            Public Property Column As Integer
            Public Property Row As Integer
            Public Property ColumnSpan As Integer = 1
            Public Property RowSpan As Integer = 1
        End Class

        Private ReadOnly _placements As New Dictionary(Of Control, Placement)()

        Public Sub New()
            LayoutMode = ModernPanel.LayoutModeEnum.Absolute
            BackColor = Color.Transparent
            BackColor1 = Color.Transparent
            BorderSize = 0
            Margin = Padding.Empty
            Padding = Padding.Empty
        End Sub

        Public Property ColumnCount As Integer = 1
        Public Property RowCount As Integer = 1
        Public ReadOnly Property ColumnStyles As New List(Of ColumnStyle)()
        Public ReadOnly Property RowStyles As New List(Of RowStyle)()

        Public Sub AddAt(control As Control, column As Integer, row As Integer)
            If control Is Nothing Then Return
            Dim placement = GetOrCreatePlacement(control)
            placement.Column = Math.Max(0, column)
            placement.Row = Math.Max(0, row)
            control.Dock = DockStyle.None
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Public Sub SetColumnSpan(control As Control, span As Integer)
            If control Is Nothing Then Return
            Dim placement = GetOrCreatePlacement(control)
            placement.ColumnSpan = Math.Max(1, span)
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Public Sub SetRowSpan(control As Control, span As Integer)
            If control Is Nothing Then Return
            Dim placement = GetOrCreatePlacement(control)
            placement.RowSpan = Math.Max(1, span)
            If Not Controls.Contains(control) Then Controls.Add(control)
            PerformLayout()
        End Sub

        Private Function GetOrCreatePlacement(control As Control) As Placement
            Dim placement As Placement = Nothing
            If Not _placements.TryGetValue(control, placement) Then
                placement = New Placement()
                _placements(control) = placement
            End If
            Return placement
        End Function

        Protected Overrides Sub OnControlRemoved(e As ControlEventArgs)
            If e.Control IsNot Nothing AndAlso _placements IsNot Nothing Then _placements.Remove(e.Control)
            MyBase.OnControlRemoved(e)
        End Sub

        Private Shared Function ResolveColumnSizes(styles As List(Of ColumnStyle), count As Integer,
                                                   available As Integer, scale As Single) As Integer()
            Dim actualCount = Math.Max(1, count)
            Dim sizes(actualCount - 1) As Integer
            Dim fixedTotal As Single = 0
            Dim percentTotal As Single = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New ColumnStyle(SizeType.Percent, 100.0F / actualCount))
                If style.SizeType = SizeType.Absolute Then
                    fixedTotal += Math.Max(0, style.Width * scale)
                ElseIf style.SizeType = SizeType.Percent Then
                    percentTotal += Math.Max(0, style.Width)
                Else
                    percentTotal += 1.0F
                End If
            Next
            Dim remaining = Math.Max(0, available - CInt(Math.Round(fixedTotal)))
            Dim used As Integer = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New ColumnStyle(SizeType.Percent, 100.0F / actualCount))
                Dim size As Integer
                If style.SizeType = SizeType.Absolute Then
                    size = Math.Max(0, CInt(Math.Round(style.Width * scale)))
                Else
                    Dim weight = If(style.SizeType = SizeType.Percent, Math.Max(0, style.Width), 1.0F)
                    size = If(percentTotal > 0, CInt(Math.Round(remaining * weight / percentTotal)), 0)
                End If
                If i = actualCount - 1 Then size = Math.Max(0, available - used)
                sizes(i) = size
                used += size
            Next
            Return sizes
        End Function

        Private Shared Function ResolveRowSizes(styles As List(Of RowStyle), count As Integer,
                                                 available As Integer, scale As Single) As Integer()
            Dim actualCount = Math.Max(1, count)
            Dim sizes(actualCount - 1) As Integer
            Dim fixedTotal As Single = 0
            Dim percentTotal As Single = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New RowStyle(SizeType.Percent, 100.0F / actualCount))
                If style.SizeType = SizeType.Absolute Then
                    fixedTotal += Math.Max(0, style.Height * scale)
                ElseIf style.SizeType = SizeType.Percent Then
                    percentTotal += Math.Max(0, style.Height)
                Else
                    percentTotal += 1.0F
                End If
            Next
            Dim remaining = Math.Max(0, available - CInt(Math.Round(fixedTotal)))
            Dim used As Integer = 0
            For i As Integer = 0 To actualCount - 1
                Dim style = If(i < styles.Count, styles(i), New RowStyle(SizeType.Percent, 100.0F / actualCount))
                Dim size As Integer
                If style.SizeType = SizeType.Absolute Then
                    size = Math.Max(0, CInt(Math.Round(style.Height * scale)))
                Else
                    Dim weight = If(style.SizeType = SizeType.Percent, Math.Max(0, style.Height), 1.0F)
                    size = If(percentTotal > 0, CInt(Math.Round(remaining * weight / percentTotal)), 0)
                End If
                If i = actualCount - 1 Then size = Math.Max(0, available - used)
                sizes(i) = size
                used += size
            Next
            Return sizes
        End Function

        Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
            MyBase.OnLayout(levent)
            ' ModernPanel 的基类构造函数可能在派生字段初始化前触发布局。
            If _placements Is Nothing OrElse _placements.Count = 0 Then Return

            Dim display = DisplayRectangle
            Dim columns = ResolveColumnSizes(ColumnStyles, ColumnCount, display.Width, LayoutScale.Width)
            Dim rows = ResolveRowSizes(RowStyles, RowCount, display.Height, LayoutScale.Height)
            Dim columnOffsets(Math.Max(0, columns.Length - 1)) As Integer
            Dim rowOffsets(Math.Max(0, rows.Length - 1)) As Integer
            For i As Integer = 1 To columns.Length - 1
                columnOffsets(i) = columnOffsets(i - 1) + columns(i - 1)
            Next
            For i As Integer = 1 To rows.Length - 1
                rowOffsets(i) = rowOffsets(i - 1) + rows(i - 1)
            Next

            For Each pair In _placements
                Dim control = pair.Key
                If control.IsDisposed Then Continue For
                Dim placement = pair.Value
                Dim c = Math.Min(Math.Max(0, placement.Column), columns.Length - 1)
                Dim r = Math.Min(Math.Max(0, placement.Row), rows.Length - 1)
                Dim cEnd = Math.Min(columns.Length, c + Math.Max(1, placement.ColumnSpan))
                Dim rEnd = Math.Min(rows.Length, r + Math.Max(1, placement.RowSpan))
                Dim bounds = New Rectangle(display.Left + columnOffsets(c),
                                           display.Top + rowOffsets(r),
                                           Math.Max(0, columnOffsets(cEnd - 1) + columns(cEnd - 1) - columnOffsets(c)),
                                           Math.Max(0, rowOffsets(rEnd - 1) + rows(rEnd - 1) - rowOffsets(r)))
                Dim margin = control.Margin
                bounds = New Rectangle(bounds.Left + margin.Left,
                                       bounds.Top + margin.Top,
                                       Math.Max(0, bounds.Width - margin.Horizontal),
                                       Math.Max(0, bounds.Height - margin.Vertical))
                control.SetBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height)
            Next
        End Sub
    End Class

    ''' <summary>固定/比例列布局，用于 LakeUI 控件之间的横向字段行。</summary>

End Namespace
