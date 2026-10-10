Imports System
Imports System.IO
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports PresetCommunity

Module Program
    Sub Main(args As String())
        RunAsync(args(0)).GetAwaiter().GetResult()
    End Sub

    Private Async Function RunAsync(directory As String) As Task
        Dim files = Await PresetRepository.ListAsync(CancellationToken.None)
        If files.Count < 9 Then Throw New Exception("远端预设不完整")
        Console.WriteLine("LIST_PASS " & files.Count)
        Dim coordinator As New ModelDownloadCoordinator()
        Dim result = Await coordinator.RunGroupAsync("Preset", files.Select(Function(file) file.RelativePath).ToList(),
            Async Function(path)
                Await PresetRepository.InstallAsync(files.First(Function(file) file.RelativePath = path), directory, Nothing, CancellationToken.None)
                Return ModelDownloadCoordinator.ItemOutcome.Succeeded
            End Function, Function(outcome) outcome, Sub(path, outcome)
                                                    End Sub, Sub(batch)
                                                             End Sub, Function() False)
        If result.Succeeded <> files.Count Then Throw New Exception("批量下载失败")
        If files.Any(Function(file) Not PresetRepository.IsCurrent(file, directory)) Then Throw New Exception("哈希不一致")
        Console.WriteLine("BATCH_HASH_PASS " & result.Summary)
        Dim first = files(0)
        Dim target = Path.Combine(directory, first.Name)
        Dim original = IO.File.ReadAllBytes(target)
        Dim bad = New PresetFile With {.Name = first.Name, .RelativePath = first.RelativePath, .Size = first.Size, .Sha256 = New String("0"c, 64)}
        Try
            Await PresetRepository.InstallAsync(bad, directory, Nothing, CancellationToken.None)
            Throw New Exception("错误哈希未被拒绝")
        Catch ex As IOException When ex.Message.Contains("SHA-256")
        End Try
        If Not original.SequenceEqual(IO.File.ReadAllBytes(target)) Then Throw New Exception("校验失败覆盖了原文件")
        Console.WriteLine("BAD_HASH_PRESERVES_ORIGINAL_PASS")
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Try
                Await PresetRepository.InstallAsync(first, directory, Nothing, cancellation.Token)
                Throw New Exception("取消未生效")
            Catch ex As OperationCanceledException
            End Try
        End Using
        If IO.Directory.GetFiles(directory, "*.partial").Length <> 0 Then Throw New Exception("残留临时文件")
        Console.WriteLine("CANCEL_CLEANUP_PASS")
        Await PresetRepository.InstallAsync(first, directory, Nothing, CancellationToken.None)
        If Not IO.Directory.GetFiles(directory, first.Name & ".*.bak").Any() Then Throw New Exception("更新未备份")
        Console.WriteLine("UPDATE_BACKUP_PASS")
    End Function
End Module
