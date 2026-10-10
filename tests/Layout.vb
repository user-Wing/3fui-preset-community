Imports System
Imports System.Drawing
Imports System.Reflection
Imports System.Windows.Forms
Imports PresetCommunity

Module LayoutTest
    <STAThread>
    Sub Main(args As String())
        Dim targetDpi = Integer.Parse(args(0))
        Application.SetHighDpiMode(If(targetDpi = 96, HighDpiMode.DpiUnaware, HighDpiMode.PerMonitorV2))
        Using form As New Form With {.AutoScaleMode = AutoScaleMode.Dpi, .AutoScaleDimensions = New SizeF(96, 96), .Font = New Font("Microsoft YaHei UI", 10), .ClientSize = New Size(1800, 1200)}
            Using panel As New PresetPanel()
                form.Controls.Add(panel)
                Dim handle = form.Handle
                panel.CreateControl()
                form.PerformAutoScale()
                Dim ratio = CSng(targetDpi / CDbl(panel.DeviceDpi))
                If ratio <> 1 Then panel.Scale(New SizeF(ratio, ratio))
                For Each logicalSize In {New Size(800, 520), New Size(1024, 768), New Size(1600, 900), New Size(800, 520)}
                    form.ClientSize = New Size(CInt(logicalSize.Width * targetDpi / 96.0), CInt(logicalSize.Height * targetDpi / 96.0))
                    form.PerformLayout()
                    panel.PerformLayout()
                    CheckLayout(panel, targetDpi)
                    Console.WriteLine($"LAYOUT_PASS dpi={targetDpi} deviceDpi={panel.DeviceDpi} size={form.ClientSize}")
                Next
            End Using
        End Using
    End Sub

    Private Sub CheckLayout(panel As PresetPanel, dpi As Integer)
        Dim refresh = FieldControl(panel, "_refresh")
        Dim all = FieldControl(panel, "_all")
        Dim list = FieldControl(panel, "_list")
        Dim status = FieldControl(panel, "_status")
        Dim canvas = FieldControl(panel, "ModernPanel1")
        Dim heading = refresh.Parent.Controls(0)
        If Math.Abs(refresh.Parent.Height - 36 * dpi / 96.0) > 1 Then Throw New Exception("标题行未按 DPI 缩放")
        If Math.Abs(refresh.Height - 28 * dpi / 96.0) > 1 Then Throw New Exception("按钮高度未按 DPI 缩放")
        If canvas.Padding.Left <> CInt(20 * dpi / 96.0) Then Throw New Exception("外层间距不符合模型下载页")
        Dim controls = {heading, refresh, all, list, status}
        For i = 0 To controls.Length - 1
            Dim bounds = BoundsInPanel(controls(i), panel)
            If Not panel.ClientRectangle.Contains(bounds) Then Throw New Exception("控件超出页面：" & controls(i).GetType().Name)
            For j = i + 1 To controls.Length - 1
                If bounds.IntersectsWith(BoundsInPanel(controls(j), panel)) Then Throw New Exception("控件重叠：" & controls(i).GetType().Name & "/" & controls(j).GetType().Name)
            Next
        Next
    End Sub

    Private Function FieldControl(panel As PresetPanel, name As String) As Control
        Return DirectCast(GetType(PresetPanel).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(panel), Control)
    End Function

    Private Function BoundsInPanel(control As Control, panel As Control) As Rectangle
        Dim bounds = control.Bounds
        Dim parent = control.Parent
        While parent IsNot panel
            bounds.Offset(parent.Location)
            parent = parent.Parent
        End While
        Return bounds
    End Function
End Module
