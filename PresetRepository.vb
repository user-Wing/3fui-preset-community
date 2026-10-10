Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Namespace PresetCommunity
    Friend NotInheritable Class PresetFile
        Public Property Name As String
        Public Property RelativePath As String
        Public Property Size As Long
        Public Property Sha256 As String
    End Class

    Friend NotInheritable Class PresetRepository
        Friend Const Root As String = "ffmpegfreeui/Preset/"
        Private Const Api As String = "https://www.modelscope.cn/api/v1/datasets/ARXChem/Software-List/repo"
        Private Shared ReadOnly Client As New HttpClient With {.Timeout = TimeSpan.FromSeconds(45)}

        Friend Shared Async Function ListAsync(token As CancellationToken) As Task(Of List(Of PresetFile))
            Dim files As New List(Of PresetFile)
            Dim page = 1
            Do
                Dim url = Api & "/tree?Revision=master&Root=" & Uri.EscapeDataString(Root.TrimEnd("/"c)) & "&PageSize=100&PageNumber=" & page
                Using response = Await Client.GetAsync(url, token)
                    response.EnsureSuccessStatusCode()
                    Using doc = JsonDocument.Parse(Await response.Content.ReadAsStringAsync(token))
                        If doc.RootElement.GetProperty("Code").GetInt32() <> 200 Then Throw New IOException("读取预设清单失败")
                        Dim data = doc.RootElement.GetProperty("Data")
                        For Each item In data.GetProperty("Files").EnumerateArray()
                            Dim name = item.GetProperty("Name").GetString()
                            Dim path = item.GetProperty("Path").GetString()
                            If item.GetProperty("Type").GetString() <> "blob" OrElse Not name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) Then Continue For
                            If name <> IO.Path.GetFileName(name) OrElse name.IndexOfAny(IO.Path.GetInvalidFileNameChars()) >= 0 OrElse path <> Root & name Then Throw New IOException("预设路径无效")
                            files.Add(New PresetFile With {.Name = name, .RelativePath = path,
                                .Size = item.GetProperty("Size").GetInt64(), .Sha256 = item.GetProperty("Sha256").GetString()})
                        Next
                        If page * 100 >= data.GetProperty("TotalCount").GetInt32() Then Exit Do
                    End Using
                End Using
                page += 1
            Loop
            Return files
        End Function

        Friend Shared Function IsCurrent(file As PresetFile, directory As String) As Boolean
            Dim target = Path.Combine(directory, file.Name)
            If Not IO.File.Exists(target) Then Return False
            Using stream = IO.File.OpenRead(target)
                Return Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)
            End Using
        End Function

        Friend Shared Async Function InstallAsync(file As PresetFile, directory As String, progress As IProgress(Of Integer), token As CancellationToken) As Task
            IO.Directory.CreateDirectory(directory)
            Dim target = Path.Combine(directory, file.Name)
            Dim temporary = target & "." & Guid.NewGuid().ToString("N") & ".partial"
            Try
                Using response = Await Client.GetAsync(Api & "?Revision=master&FilePath=" & Uri.EscapeDataString(file.RelativePath), HttpCompletionOption.ResponseHeadersRead, token)
                    response.EnsureSuccessStatusCode()
                    Using input = Await response.Content.ReadAsStreamAsync(token), output = New FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, True)
                        Dim buffer(81919) As Byte
                        Dim received As Long = 0
                        Do
                            Dim count = Await input.ReadAsync(buffer.AsMemory(), token)
                            If count = 0 Then Exit Do
                            Await output.WriteAsync(buffer.AsMemory(0, count), token)
                            received += count
                            If file.Size > 0 Then progress?.Report(CInt(Math.Min(100, received * 100 / file.Size)))
                        Loop
                        If received <> file.Size Then Throw New IOException("预设文件大小校验失败")
                    End Using
                End Using
                Using stream = IO.File.OpenRead(temporary)
                    If Not Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase) Then Throw New IOException("预设 SHA-256 校验失败")
                    stream.Position = 0
                    Using doc = JsonDocument.Parse(stream)
                        If doc.RootElement.GetProperty("预设文件版本").GetInt32() <> 6 Then Throw New IOException("仅支持 v6 预设")
                    End Using
                End Using
                token.ThrowIfCancellationRequested()
                ' 保留原文件，校验成功后才替换正式预设。
                If IO.File.Exists(target) Then
                    IO.File.Replace(temporary, target, target & "." & DateTime.Now.ToString("yyyyMMddHHmmssfff") & ".bak")
                Else
                    IO.File.Move(temporary, target)
                End If
            Finally
                If IO.File.Exists(temporary) Then IO.File.Delete(temporary)
            End Try
        End Function
    End Class
End Namespace
