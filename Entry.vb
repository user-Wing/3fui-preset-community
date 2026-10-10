Imports System
Imports System.Windows.Forms

Namespace PresetCommunity
    Public Class Entry
        Private Shared _addPanel As Action(Of String, Control)

        Public Shared Sub SetHost_AddCustomWinformPanel(callback As Action(Of String, Control))
            _addPanel = callback
        End Sub

        Public Shared Sub Entry()
            If _addPanel Is Nothing Then Throw New InvalidOperationException("宿主未注入面板回调")
            _addPanel("预设社区", New PresetPanel())
        End Sub
    End Class
End Namespace
