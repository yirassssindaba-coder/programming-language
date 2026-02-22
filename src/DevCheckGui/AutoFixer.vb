Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

' Auto remediation for Missing/Error tools:
' 1) Try discover in well-known directories and add to PATH (User + Process)
' 2) Retry if TIMEOUT (increase timeout)
' 3) Ensure PATH for resolved exe directory
' 4) Attempt install (winget/choco/scoop/npm) and re-check
Public NotInheritable Class AutoFixer

    Public Shared Async Function FixAsync(spec As ToolSpec, current As ToolResult, token As CancellationToken) As Task(Of ToolResult)
        Dim notes As New List(Of String)()
        Dim res As ToolResult = current

        If spec Is Nothing Then Return current

        ' 0) Try fix PATH for well-known install directories (common on Windows)
        If res.Status <> ToolStatus.Available Then
            Dim found As String = PathHelper.TryFindInWellKnownDirs(spec.Command)
            If Not String.IsNullOrWhiteSpace(found) Then
                notes.Add("[AUTO-FIX] Found in well-known dir: " & found)
                Try
                    Dim d As String = Path.GetDirectoryName(found)
                    If Not String.IsNullOrWhiteSpace(d) Then
                        PathHelper.EnsureUserAndProcessPathContains(d)
                        notes.Add("[AUTO-FIX] PATH ensured: " & d)
                    End If
                Catch
                End Try

                Dim re0 As ToolResult = Await CommandRunner.RunAsync(spec, token)
                re0.FullOutput = String.Join(Environment.NewLine, notes) & Environment.NewLine & "----" & Environment.NewLine & If(re0.FullOutput, "")
                If re0.Status = ToolStatus.Available Then Return re0
                res = re0
            End If
        End If

        ' 1) If TIMEOUT, retry with longer timeout once
        If res.Status = ToolStatus.Error AndAlso (If(res.FullOutput, "")).Contains("[TIMEOUT]") Then
            notes.Add("[AUTO-FIX] Timeout detected. Retrying with longer timeout…")

            Dim spec2 As ToolSpec = CloneSpec(spec)
            spec2.TimeoutMs = Math.Max(spec.TimeoutMs * 2, 20000)

            Dim re1 As ToolResult = Await CommandRunner.RunAsync(spec2, token)
            re1.Spec = spec ' keep original reference for UI
            re1.FullOutput = String.Join(Environment.NewLine, notes) & Environment.NewLine & "----" & Environment.NewLine & If(re1.FullOutput, "")
            If re1.Status = ToolStatus.Available Then Return re1
            res = re1
        End If

        ' 2) If we have a resolved path, ensure its directory is added to PATH, then re-check
        If res.Status <> ToolStatus.Available AndAlso Not String.IsNullOrWhiteSpace(res.ResolvedPath) Then
            Try
                Dim dir As String = Path.GetDirectoryName(res.ResolvedPath)
                If Not String.IsNullOrWhiteSpace(dir) Then
                    If PathHelper.EnsureUserAndProcessPathContains(dir) Then
                        notes.Add("[AUTO-FIX] PATH ensured from resolved exe: " & dir)

                        ' Special case: per-user Elixir install layout (.elixir-install)
                        ' Ensure both Elixir and Erlang bins are on PATH permanently.
                        Try
                            Dim cmdLower As String = If(spec.Command, "").Trim().ToLowerInvariant()
                            If cmdLower = "elixir" OrElse String.Equals(spec.Name, "Elixir", StringComparison.OrdinalIgnoreCase) Then
                                Dim rp As String = res.ResolvedPath.Trim().Trim(""""c)
                                If rp.ToLowerInvariant().Contains("\\.elixir-install\\installs\\") Then
                                    Dim binDir As String = Path.GetDirectoryName(rp)
                                    If Not String.IsNullOrWhiteSpace(binDir) Then
                                        If PathHelper.EnsureUserAndProcessPathContains(binDir) Then
                                            notes.Add("[AUTO-FIX] Elixir bin added to PATH: " & binDir)
                                        End If
                                    End If

                                    Dim elixirHome As String = ""
                                    Try
                                        elixirHome = Directory.GetParent(binDir).FullName
                                    Catch
                                    End Try

                                    If Not String.IsNullOrWhiteSpace(elixirHome) Then
                                        Dim p1 = Directory.GetParent(elixirHome) ' ...\installs\elixir
                                        Dim installsDir = If(p1 IsNot Nothing, Directory.GetParent(p1.FullName), Nothing) ' ...\installs
                                        If installsDir IsNot Nothing Then
                                            Dim erlangRoot = Path.Combine(installsDir.FullName, "erlang")
                                            If Directory.Exists(erlangRoot) Then
                                                Dim dirs = Directory.GetDirectories(erlangRoot)
                                                If dirs IsNot Nothing AndAlso dirs.Length > 0 Then
                                                    Array.Sort(dirs, StringComparer.OrdinalIgnoreCase)
                                                    Dim erlangHome = dirs(dirs.Length - 1)
                                                    Dim erlangBin = Path.Combine(erlangHome, "bin")
                                                    If Directory.Exists(erlangBin) Then
                                                        If PathHelper.EnsureUserAndProcessPathContains(erlangBin) Then
                                                            notes.Add("[AUTO-FIX] Erlang bin added to PATH: " & erlangBin)
                                                        End If
                                                    End If
                                                End If
                                            End If
                                        End If
                                    End If
                                End If
                            End If
                        Catch
                        End Try

                        Dim re2 As ToolResult = Await CommandRunner.RunAsync(spec, token)
                        re2.FullOutput = String.Join(Environment.NewLine, notes) & Environment.NewLine & "----" & Environment.NewLine & If(re2.FullOutput, "")
                        If re2.Status = ToolStatus.Available Then Return re2
                        res = re2
                    End If
                End If
            Catch
            End Try
        End If

        ' 3) Attempt install if still Missing/Error
        If res.Status <> ToolStatus.Available AndAlso Not token.IsCancellationRequested Then
            notes.Add("[AUTO-FIX] Attempting install…")
            Dim ir As InstallResult = Await InstallerRunner.InstallAsync(spec, token)

            Dim after As ToolResult = Await CommandRunner.RunAsync(spec, token)
            Dim installHeader As String = $"[AUTO-INSTALL] Provider={ir.ProviderUsed} Success={ir.Success} ExitCode={If(ir.ExitCode.HasValue, ir.ExitCode.Value.ToString(), "-")}"
            after.FullOutput = String.Join(Environment.NewLine, notes) & Environment.NewLine &
                              installHeader & Environment.NewLine &
                              If(ir.Output, "") & Environment.NewLine &
                              Environment.NewLine & "----" & Environment.NewLine &
                              If(after.FullOutput, "")

            ' Ensure PATH for resolved exe
            If Not String.IsNullOrWhiteSpace(after.ResolvedPath) Then
                Try
                    Dim dir As String = Path.GetDirectoryName(after.ResolvedPath)
                    If Not String.IsNullOrWhiteSpace(dir) Then
                        PathHelper.EnsureUserAndProcessPathContains(dir)
                    End If
                Catch
                End Try
            End If

            Return after
        End If

        ' If nothing worked, still return with notes appended
        If notes.Count > 0 Then
            res.FullOutput = String.Join(Environment.NewLine, notes) & Environment.NewLine & "----" & Environment.NewLine & If(res.FullOutput, "")
        End If

        Return res
    End Function

    Private Shared Function CloneSpec(spec As ToolSpec) As ToolSpec
        Dim s As New ToolSpec()
        s.Name = spec.Name
        s.Command = spec.Command
        s.Args = spec.Args
        s.Category = spec.Category
        s.StdinText = spec.StdinText
        s.TimeoutMs = spec.TimeoutMs
        Return s
    End Function

End Class
