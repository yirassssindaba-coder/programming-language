Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks

Public NotInheritable Class CommandRunner

    Public Shared Function ResolveExecutable(command As String) As String
        If String.IsNullOrWhiteSpace(command) Then Return ""

        Dim cmdRaw As String = command.Trim()

        Dim cmd As String = ""
        Try
            cmd = Environment.ExpandEnvironmentVariables(cmdRaw).Trim().Trim(""""c)
        Catch
            cmd = cmdRaw.Trim().Trim(""""c)
        End Try

        ' If user passed a path (contains directory separators or drive colon), treat as path.
        Dim looksLikePath As Boolean =
            cmd.Contains(System.IO.Path.DirectorySeparatorChar) OrElse
            cmd.Contains(System.IO.Path.AltDirectorySeparatorChar) OrElse
            cmd.Contains(":"c)

        If looksLikePath Then
            If File.Exists(cmd) Then Return cmd

            ' Try add PATHEXT if missing extension
            Dim ext0 As String = System.IO.Path.GetExtension(cmd)
            If String.IsNullOrWhiteSpace(ext0) Then
                Dim pathextStr0 As String = Environment.GetEnvironmentVariable("PATHEXT")
                If String.IsNullOrWhiteSpace(pathextStr0) Then pathextStr0 = ".EXE;.CMD;.BAT;.COM"

                For Each ext In pathextStr0.Split(";"c, StringSplitOptions.RemoveEmptyEntries)
                    Dim candidate As String = cmd & ext.Trim()
                    If File.Exists(candidate) Then Return candidate
                Next
            End If

            Return ""
        End If

        ' Candidate extensions from PATHEXT
        Dim pathextStr As String = Environment.GetEnvironmentVariable("PATHEXT")
        If String.IsNullOrWhiteSpace(pathextStr) Then pathextStr = ".EXE;.CMD;.BAT;.COM"

        Dim pathext As New List(Of String)()
        For Each e In pathextStr.Split(";"c, StringSplitOptions.RemoveEmptyEntries)
            Dim ee As String = e.Trim()
            If ee.Length = 0 Then Continue For
            If Not ee.StartsWith(".", StringComparison.Ordinal) Then ee = "." & ee
            pathext.Add(ee)
        Next

        Dim hasExt As Boolean = Not String.IsNullOrWhiteSpace(System.IO.Path.GetExtension(cmd))

        ' Search combined PATH (Process + User + Machine) for maximum parity with PowerShell/Get-Command
        Dim parts As List(Of String) = GetCombinedPathParts()

        For Each part In parts
            Try
                If hasExt Then
                    Dim candidate As String = System.IO.Path.Combine(part, cmd)
                    If File.Exists(candidate) Then Return candidate
                Else
                    For Each ext In pathext
                        Dim candidate As String = System.IO.Path.Combine(part, cmd & ext)
                        If File.Exists(candidate) Then Return candidate
                    Next
                End If
            Catch
            End Try
        Next

        ' Fallback: use where.exe (sometimes picks up shims better than manual parsing)
        Dim whereResolved As String = TryResolveWithWhere(cmd)
        If Not String.IsNullOrWhiteSpace(whereResolved) Then Return whereResolved

        ' Also check current directory (rare but safe)
        Try
            Dim cur As String = Environment.CurrentDirectory
            If hasExt Then
                Dim candidate As String = System.IO.Path.Combine(cur, cmd)
                If File.Exists(candidate) Then Return candidate
            Else
                For Each ext In pathext
                    Dim candidate As String = System.IO.Path.Combine(cur, cmd & ext)
                    If File.Exists(candidate) Then Return candidate
                Next
            End If
        Catch
        End Try

        Return ""
    End Function

    Private Shared Function GetCombinedPathParts() As List(Of String)
        Dim parts As New List(Of String)()
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For Each target In New EnvironmentVariableTarget() {EnvironmentVariableTarget.Process, EnvironmentVariableTarget.User, EnvironmentVariableTarget.Machine}
            Dim raw As String = ""
            Try
                raw = Environment.GetEnvironmentVariable("Path", target)
            Catch
            End Try

            If String.IsNullOrWhiteSpace(raw) Then Continue For

            For Each piece In raw.Split(";"c, StringSplitOptions.RemoveEmptyEntries)
                Dim pp As String = piece.Trim().Trim(""""c)
                If pp.Length = 0 Then Continue For

                Try
                    pp = Environment.ExpandEnvironmentVariables(pp).Trim()
                Catch
                End Try

                pp = pp.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                If pp.Length = 0 Then Continue For

                If seen.Add(pp) Then parts.Add(pp)
            Next
        Next

        Return parts
    End Function

    Private Shared Function TryResolveWithWhere(cmd As String) As String
        Try
            Dim psi As New ProcessStartInfo() With {
                .FileName = "where",
                .Arguments = cmd,
                .UseShellExecute = False,
                .RedirectStandardOutput = True,
                .RedirectStandardError = True,
                .CreateNoWindow = True
            }

            Using p As Process = Process.Start(psi)
                If p Is Nothing Then Return ""

                Dim output As String = p.StandardOutput.ReadToEnd()
                Dim err As String = p.StandardError.ReadToEnd()

                Try
                    p.WaitForExit(2000)
                Catch
                End Try

                Dim lines As String() = (If(output, "") & vbCrLf & If(err, "")).Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
                For Each line In lines
                    Dim candidate As String = line.Trim()
                    If candidate.Length = 0 Then Continue For

                    ' where.exe may return extensionless stubs (ex: C:\Windows\System32\php)
                    ' which will fail to execute (Win32Exception 193). Only accept real executables.
                    Dim ext As String = Path.GetExtension(candidate).ToLowerInvariant()
                    If ext.Length = 0 Then Continue For

                    Dim pathext As String = Environment.GetEnvironmentVariable("PATHEXT")
                    Dim ok As Boolean = False
                    If Not String.IsNullOrWhiteSpace(pathext) Then
                        For Each pe In pathext.Split(";"c)
                            Dim e As String = pe.Trim().ToLowerInvariant()
                            If e = ext Then ok = True : Exit For
                        Next
                    Else
                        ok = (ext = ".exe" OrElse ext = ".cmd" OrElse ext = ".bat" OrElse ext = ".com")
                    End If
                    If Not ok Then Continue For

                    If File.Exists(candidate) Then Return candidate
                Next
            End Using
        Catch
        End Try

        Return ""
    End Function

    Private Shared Function NormalizePathSegment(segment As String) As String
        Dim s As String = If(segment, "").Trim()
        If s.Length = 0 Then Return ""

        ' Remove wrapping quotes (common in PATH entries like "C:\Program Files\...")
        s = s.Trim().Trim(""""c)

        ' Expand %VAR% segments (common for npm/cargo/etc)
        Try
            s = Environment.ExpandEnvironmentVariables(s)
        Catch
        End Try

        Return s.Trim()
    End Function


    Public Shared Async Function RunAsync(spec As ToolSpec, token As CancellationToken) As Task(Of ToolResult)
        Dim res As New ToolResult With {
            .Spec = spec,
            .StartedAtUtc = DateTime.UtcNow
        }

        Dim resolved As String = ResolveExecutable(spec.Command)
        res.ResolvedPath = resolved

        If String.IsNullOrEmpty(resolved) Then
            res.Status = ToolStatus.Missing
            res.VersionLine = "TIDAK PUNYA"
            res.FullOutput = ""
            res.ExitCode = Nothing
            res.DurationMs = CInt((DateTime.UtcNow - res.StartedAtUtc).TotalMilliseconds)
            Return res
        End If

        Dim runFile As String = resolved
        Dim runArgs As String = If(spec.Args, "")
        Dim useStdin As Boolean = (spec.StdinText IsNot Nothing)

        ' If resolved is a script wrapper (.cmd/.bat), run via cmd.exe using the canonical quoting:
        '   cmd.exe /d /s /c ""<script>" <args>""
        ' This avoids the classic Windows error: '""C:\\Program' is not recognized...'
        Dim ext As String = ""
        Try
            ext = System.IO.Path.GetExtension(resolved).ToLowerInvariant()
        Catch
        End Try

        If ext = ".cmd" OrElse ext = ".bat" Then
            Dim exeNoQuotes As String = resolved.Trim().Trim(""""c)

            Dim comspec As String = ""
            Try
                comspec = Environment.GetEnvironmentVariable("ComSpec")
            Catch
            End Try
            If Not String.IsNullOrWhiteSpace(comspec) Then
                comspec = comspec.Trim().Trim(""""c)
            End If
            If String.IsNullOrWhiteSpace(comspec) Then comspec = "cmd.exe"

            runFile = comspec

            ' More robust pattern (avoids weird ""C:\Program" parsing with some .cmd/.bat):
            ' cmd.exe /d /s /c ""call "<path>" <args>""
            Dim innerCmd As String = "call " & """" & exeNoQuotes & """"
            If Not String.IsNullOrWhiteSpace(runArgs) Then
                innerCmd &= " " & runArgs
            End If

            runArgs = "/d /s /c " & """" & innerCmd & """"
            useStdin = False
        End If

        Dim psi As New ProcessStartInfo() With {
            .FileName = runFile,
            .Arguments = runArgs,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .RedirectStandardInput = useStdin,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .WorkingDirectory = Environment.CurrentDirectory
        }

        ' Tool-specific environment fixes (mostly for Windows batch-based installs)
        ApplyToolSpecificEnvironment(spec, res, psi)

        Using p As New Process()
            p.StartInfo = psi

            Try
                If Not p.Start() Then
                    res.Status = ToolStatus.Error
                    res.VersionLine = "ERROR: gagal start"
                    res.FullOutput = "Process failed to start."
                    res.ExitCode = Nothing
                    res.DurationMs = CInt((DateTime.UtcNow - res.StartedAtUtc).TotalMilliseconds)
                    Return res
                End If
            Catch ex As Exception
                res.Status = ToolStatus.Error
                res.VersionLine = "ERROR: tidak bisa start"
                res.FullOutput = ex.ToString()
                res.ExitCode = Nothing
                res.DurationMs = CInt((DateTime.UtcNow - res.StartedAtUtc).TotalMilliseconds)
                Return res
            End Try

            ' Write stdin if needed (tclsh)
            If spec.StdinText IsNot Nothing Then
                Try
                    Await p.StandardInput.WriteAsync(spec.StdinText)
                Catch
                End Try

                Try
                    p.StandardInput.Close()
                Catch
                End Try
            End If

            Dim stdoutTask As Task(Of String) = p.StandardOutput.ReadToEndAsync()
            Dim stderrTask As Task(Of String) = p.StandardError.ReadToEndAsync()

            Dim timeoutMs As Integer = Math.Max(500, spec.TimeoutMs)
            Dim exitTask As Task = p.WaitForExitAsync(token)
            Dim delayTask As Task = Task.Delay(timeoutMs, token)

            Dim finished As Task = Await Task.WhenAny(exitTask, delayTask)

            If finished Is delayTask Then
                ' timeout
                Try
                    p.Kill(True)
                Catch
                End Try

                Dim so As String = ""
                Dim se As String = ""
                Try : so = Await stdoutTask : Catch : End Try
                Try : se = Await stderrTask : Catch : End Try

                Dim combined As String = CombineOutputs(so, se)

                res.Status = ToolStatus.Error
                res.ExitCode = Nothing
                res.FullOutput = combined & If(combined.Trim().Length > 0, vbCrLf, "") & "[TIMEOUT] " & timeoutMs & " ms"
                res.VersionLine = PickFirstLine(res.FullOutput)
                res.DurationMs = CInt((DateTime.UtcNow - res.StartedAtUtc).TotalMilliseconds)

                ' Final tool-specific classification tweaks (eg OCaml initializers).
                ApplyPostStatusFixes(spec, res)
                Return res
            End If

            ' Wait completed (or token cancelled)
            If token.IsCancellationRequested Then
                Try
                    p.Kill(True)
                Catch
                End Try
            End If

            Dim stdout As String = Await stdoutTask
            Dim stderr As String = Await stderrTask

            res.ExitCode = p.ExitCode
            res.FullOutput = CombineOutputs(stdout, stderr)
            res.VersionLine = PickFirstLine(res.FullOutput)

            If p.ExitCode = 0 Then
                res.Status = ToolStatus.Available
            Else
                ' Some tools return non-zero but still print a valid version line (mimic PowerShell "2>&1" behavior).
                If LooksLikeVersionLine(res.VersionLine, res.FullOutput) Then
                    res.Status = ToolStatus.Available
                Else
                    res.Status = ToolStatus.Error
                End If
            End If

            res.DurationMs = CInt((DateTime.UtcNow - res.StartedAtUtc).TotalMilliseconds)

            ' Final tool-specific classification tweaks (eg OCaml initializers).
            ApplyPostStatusFixes(spec, res)
            Return res
        End Using
    End Function

    ' Tool-specific environment fixes (per-process only).
    ' Example: per-user Elixir installs sometimes need ERLANG_HOME / PATH assistance
    ' depending on how their .BAT computes relative paths.
    Private Shared Sub ApplyToolSpecificEnvironment(spec As ToolSpec, res As ToolResult, psi As ProcessStartInfo)
        Try
            If spec Is Nothing OrElse res Is Nothing OrElse psi Is Nothing Then Return

            Dim resolved As String = If(res.ResolvedPath, "")
            Dim cmdLower As String = If(spec.Command, "").Trim().ToLowerInvariant()

            ' Elixir per-user installer layout: %USERPROFILE%\.elixir-install\installs\...
            If cmdLower = "elixir" OrElse resolved.ToLowerInvariant().Contains("\\.elixir-install\\") Then
                ApplyElixirEnv(resolved, psi)
            End If
        Catch
            ' Best-effort only
        End Try
    End Sub

    Private Shared Sub ApplyElixirEnv(resolvedPath As String, psi As ProcessStartInfo)
        Dim p As String = If(resolvedPath, "").Trim().Trim(""""c)
        If p.Length = 0 Then Return

        Dim binDir As String = ""
        Try
            binDir = Path.GetDirectoryName(p)
        Catch
        End Try
        If String.IsNullOrWhiteSpace(binDir) Then Return

        Dim addDirs As New List(Of String)()
        addDirs.Add(binDir)

        ' ELIXIR_HOME is one level above ...\bin
        Dim elixirHome As String = ""
        Try
            elixirHome = Directory.GetParent(binDir).FullName
        Catch
        End Try
        If Not String.IsNullOrWhiteSpace(elixirHome) Then
            psi.EnvironmentVariables("ELIXIR_HOME") = elixirHome

            ' Try to locate Erlang installed by the same installer
            Try
                Dim elixirRootParent As DirectoryInfo = Directory.GetParent(elixirHome) ' ...\installs\elixir
                Dim installsDir As DirectoryInfo = If(elixirRootParent IsNot Nothing, Directory.GetParent(elixirRootParent.FullName), Nothing) ' ...\installs
                If installsDir IsNot Nothing Then
                    Dim erlangRoot As String = Path.Combine(installsDir.FullName, "erlang")
                    If Directory.Exists(erlangRoot) Then
                        Dim candidates As String() = Directory.GetDirectories(erlangRoot)
                        If candidates IsNot Nothing AndAlso candidates.Length > 0 Then
                            Array.Sort(candidates, StringComparer.OrdinalIgnoreCase)
                            Dim erlangHome As String = candidates(candidates.Length - 1)
                            psi.EnvironmentVariables("ERLANG_HOME") = erlangHome

                            Dim erlangBin As String = Path.Combine(erlangHome, "bin")
                            If Directory.Exists(erlangBin) Then addDirs.Add(erlangBin)
                        End If
                    End If
                End If
            Catch
            End Try
        End If

        PrependToProcessPath(psi, addDirs)
    End Sub

    Private Shared Sub PrependToProcessPath(psi As ProcessStartInfo, dirs As List(Of String))
        If dirs Is Nothing OrElse dirs.Count = 0 Then Return

        Dim current As String = ""
        Try
            current = If(psi.EnvironmentVariables("PATH"), "")
        Catch
        End Try
        If String.IsNullOrWhiteSpace(current) Then
            current = Environment.GetEnvironmentVariable("PATH")
        End If

        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim normalized As New List(Of String)()

        For Each d In dirs
            Dim t As String = If(d, "").Trim().Trim(""""c)
            If t.Length = 0 Then Continue For
            If Not seen.Contains(t) Then
                seen.Add(t)
                normalized.Add(t)
            End If
        Next
        If normalized.Count = 0 Then Return

        Dim newPath As String = String.Join(";", normalized)
        If Not String.IsNullOrWhiteSpace(current) Then
            newPath &= ";" & current
        End If

        psi.EnvironmentVariables("PATH") = newPath
    End Sub

    Private Shared Function QuoteForCmd(value As String) As String
        Dim s As String = If(value, "")
        s = s.Replace("""", """""")
        Return """" & s & """"
    End Function

    Private Shared Sub ApplyPostStatusFixes(spec As ToolSpec, res As ToolResult)
        If spec Is Nothing OrElse res Is Nothing Then Return

        Dim cmdLower As String = If(spec.Command, "").Trim().ToLowerInvariant()
        Dim nameLower As String = If(spec.Name, "").Trim().ToLowerInvariant()
        Dim resolved As String = If(res.ResolvedPath, "").Trim().Trim(""""c)

        ' OCaml on Windows can "exist" but take a long time on first run (DkML initialization),
        ' or fail with MSYS2 keyring issues. In those cases, don't keep it stuck in ERROR forever;
        ' infer the version from files/output and mark it as Available so Auto-Fix doesn't
        ' try to install a second OCaml distribution.
        If cmdLower = "ocamlc" OrElse nameLower.Contains("ocaml") Then
            If resolved.Length > 0 Then
                Dim rpLower As String = resolved.ToLowerInvariant()
                If rpLower.Contains("\\dkmlnative\\") OrElse rpLower.Contains("\\diskuv") Then
                    Dim v As String = InferOcamlVersion(resolved, res.FullOutput)
                    If Not String.IsNullOrWhiteSpace(v) Then
                        res.Status = ToolStatus.Available
                        res.VersionLine = "OCaml " & v

                        Dim outLower As String = If(res.FullOutput, "").ToLowerInvariant()
                        If outLower.Contains("creating it now") OrElse outLower.Contains("dkml") Then
                            res.FullOutput &= vbCrLf & "[NOTE] DkML may still be initializing the native toolchain; rerun after it completes." 
                        End If
                    End If
                End If
            End If
        End If
    End Sub

    Private Shared Function InferOcamlVersion(resolvedExePath As String, fullOutput As String) As String
        Dim output As String = If(fullOutput, "")

        ' Prefer explicit OCaml version text
        Dim m As Match = Regex.Match(output, "(?i)\\bocaml\\s+version\\s+(\\d+\\.\\d+\\.\\d+)\\b")
        If m.Success Then Return m.Groups(1).Value

        ' DkML patch logs often include: "release 4.14.2"
        m = Regex.Match(output, "(?i)\\brelease\\s+(\\d+\\.\\d+\\.\\d+)\\b")
        If m.Success Then Return m.Groups(1).Value

        ' Try VERSION file in DkML layout: <root>\src\ocaml\VERSION
        Try
            Dim binDir As String = Path.GetDirectoryName(resolvedExePath)
            Dim usrDir As String = If(Directory.GetParent(binDir), Nothing)?.FullName
            Dim rootDir As String = If(usrDir IsNot Nothing, If(Directory.GetParent(usrDir), Nothing)?.FullName, Nothing)
            If Not String.IsNullOrWhiteSpace(rootDir) Then
                Dim versionFile As String = Path.Combine(rootDir, "src", "ocaml", "VERSION")
                If File.Exists(versionFile) Then
                    Dim t As String = File.ReadAllText(versionFile)
                    m = Regex.Match(t, "\\b(\\d+\\.\\d+\\.\\d+)\\b")
                    If m.Success Then Return m.Groups(1).Value
                End If
            End If
        Catch
        End Try

        ' Finally, try file version info
        Try
            Dim fvi As FileVersionInfo = FileVersionInfo.GetVersionInfo(resolvedExePath)
            Dim v As String = If(fvi.ProductVersion, fvi.FileVersion)
            m = Regex.Match(If(v, ""), "\\b(\\d+(?:\\.\\d+){1,3})\\b")
            If m.Success Then Return m.Groups(1).Value
        Catch
        End Try

        Return ""
    End Function

    Private Shared Function LooksLikeVersionLine(firstLine As String, fullOutput As String) As Boolean
        Dim a As String = If(firstLine, "").Trim()
        Dim b As String = If(fullOutput, "").Trim()
        If a.Length = 0 AndAlso b.Length = 0 Then Return False

        Dim t As String = If(a, b).Trim()
        Dim lower As String = t.ToLowerInvariant()

        ' common "not found" patterns
        If lower.Contains("not recognized") OrElse lower.Contains("no such file") Then Return False

        ' obvious error lines
        If lower.StartsWith("error") OrElse lower.Contains("exception") Then Return False

        ' version-ish patterns
        If lower.Contains("version") Then Return True

        ' starts with v1.2.3 or 1.2.3
        Dim m As System.Text.RegularExpressions.Match =
            System.Text.RegularExpressions.Regex.Match(t, "^\s*v?\d+(\.\d+){1,}")
        If m.Success Then Return True

        ' contains x.y.z somewhere
        Dim m2 As System.Text.RegularExpressions.Match =
            System.Text.RegularExpressions.Regex.Match(b, "\d+(\.\d+){2,}")
        If m2.Success Then Return True

        Return False
    End Function

Private Shared Function CombineOutputs(stdout As String, stderr As String) As String
        Dim a As String = If(stdout, "")
        Dim b As String = If(stderr, "")
        If a.Trim().Length = 0 Then Return b.Trim()
        If b.Trim().Length = 0 Then Return a.Trim()
        Return (a.TrimEnd() & vbCrLf & b.Trim()).Trim()
    End Function

    Public Shared Function PickFirstLine(output As String) As String
        Dim s As String = If(output, "").Trim()
        If s.Length = 0 Then Return "-"

        Dim lines As String() = s.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf)
        For Each line In lines
            Dim t As String = If(line, "").Trim()
            If t.Length > 0 Then Return t
        Next

        Return "-"
    End Function

    Private Shared Function GetPathextList() As List(Of String)
        Dim pe As String = Environment.GetEnvironmentVariable("PATHEXT")
        If String.IsNullOrWhiteSpace(pe) Then pe = ".COM;.EXE;.BAT;.CMD"

        Dim parts As String() = pe.Split(";"c, StringSplitOptions.RemoveEmptyEntries)
        Dim list As New List(Of String)()

        For Each p In parts
            Dim ext As String = p.Trim()
            If ext.Length = 0 Then Continue For
            If Not ext.StartsWith("."c) Then ext = "." & ext
            list.Add(ext.ToUpperInvariant())
        Next

        ' Ensure common ones exist
        Dim common As String() = {".EXE", ".CMD", ".BAT", ".COM"}
        For Each c In common
            If Not list.Contains(c) Then list.Add(c)
        Next

        Return list
    End Function

End Class
