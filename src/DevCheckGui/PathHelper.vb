Option Strict On
Option Explicit On

Imports System
Imports System.IO

Public NotInheritable Class PathHelper

    Public Shared Function EnsureUserAndProcessPathContains(dirOrEnvPath As String) As Boolean
        Dim dir As String = NormalizeDir(dirOrEnvPath)
        If String.IsNullOrWhiteSpace(dir) Then Return False

        Dim changedUser As Boolean = EnsurePathContains(dir, EnvironmentVariableTarget.User)
        Dim changedProc As Boolean = EnsurePathContains(dir, EnvironmentVariableTarget.Process)

        ' Also refresh Process PATH from User PATH to pick up other installers that updated PATH.
        Try
            Dim userPath As String = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)
            If Not String.IsNullOrEmpty(userPath) Then
                Environment.SetEnvironmentVariable("Path", userPath, EnvironmentVariableTarget.Process)
            End If
        Catch
        End Try

        Return changedUser OrElse changedProc
    End Function

    Public Shared Function EnsurePathContains(dir As String, target As EnvironmentVariableTarget) As Boolean
        Dim d As String = NormalizeDir(dir)
        If String.IsNullOrWhiteSpace(d) Then Return False

        Dim current As String = Environment.GetEnvironmentVariable("Path", target)
        If current Is Nothing Then current = ""

        Dim parts As String() = current.Split(";"c, StringSplitOptions.RemoveEmptyEntries)
        For Each p In parts
            Dim norm As String = NormalizeDir(p)
            If norm.Equals(d, StringComparison.OrdinalIgnoreCase) Then
                Return False
            End If
        Next

        Dim newPath As String
        If current.Trim().Length = 0 Then
            newPath = d
        Else
            newPath = current.TrimEnd(";"c) & ";" & d
        End If

        Environment.SetEnvironmentVariable("Path", newPath, target)
        Return True
    End Function

    Public Shared Function NormalizeDir(value As String) As String
        Dim s As String = If(value, "").Trim()
        If s.Length = 0 Then Return ""

        s = s.Trim(""""c)

        Try
            s = Environment.ExpandEnvironmentVariables(s)
        Catch
        End Try

        s = s.Trim()
        If s.Length = 0 Then Return ""

        Try
            ' Normalize separator & remove trailing
            s = Path.GetFullPath(s)
        Catch
            ' If path is not valid, still return expanded string
        End Try

        Return s.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
    End Function



    ' Try discover a command in common install locations even if PATH is not set correctly.
    ' Returns full path if found, else "".
    Public Shared Function TryFindInWellKnownDirs(command As String) As String
        Dim cmd As String = If(command, "").Trim()
        If cmd.Length = 0 Then Return ""

        ' If command already has extension, include it; else try common extensions
        Dim candidates As New List(Of String)()
        Dim ext As String = ""
        Try
            ext = Path.GetExtension(cmd)
        Catch
        End Try

        If Not String.IsNullOrWhiteSpace(ext) Then
            candidates.Add(cmd)
        Else
            candidates.Add(cmd & ".exe")
            candidates.Add(cmd & ".cmd")
            candidates.Add(cmd & ".bat")
        End If

        ' Common directories (user-level first)
        Dim dirs As New List(Of String) From {
            "%APPDATA%\npm",
            "%USERPROFILE%\scoop\shims",
            "%USERPROFILE%\scoop\apps\php\current",
            "%USERPROFILE%\.cargo\bin",
            "%LOCALAPPDATA%\Microsoft\WindowsApps",
            "%ProgramFiles%\Git\bin",
            "%ProgramFiles%\Git\usr\bin",
            "%ProgramFiles(x86)%\Git\bin",
            "%ProgramFiles(x86)%\Git\usr\bin",
            "C:\\xampp\\php",
            "C:\\wamp64\\bin\\php",
            "C:\\laragon\\bin\\php"
        }

        For Each d In dirs
            Dim dir As String = NormalizeDir(d)
            If String.IsNullOrWhiteSpace(dir) Then Continue For
            If Not Directory.Exists(dir) Then Continue For

            For Each c In candidates
                Dim name As String = c
                If name.Contains(Path.DirectorySeparatorChar) OrElse name.Contains(Path.AltDirectorySeparatorChar) Then
                    ' if user gave path-ish command, only use filename
                    Try
                        name = Path.GetFileName(name)
                    Catch
                    End Try
                End If

                Dim full As String = ""
                Try
                    full = Path.Combine(dir, name)
                Catch
                End Try

                If Not String.IsNullOrWhiteSpace(full) AndAlso File.Exists(full) Then
                    Return full
                End If
            Next
        Next

        Return ""
    End Function

End Class
