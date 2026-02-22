Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic

Public NotInheritable Class InstallerCatalog

    ' Return an install plan for the given tool. The runner will choose the best available provider.
    Public Shared Function GetPlan(spec As ToolSpec) As InstallPlan
        Dim plan As New InstallPlan With {.DisplayName = spec.Name}

        ' NPM global installs (best for JS CLIs)
        If spec.Name.Equals("TypeScript (tsc)", StringComparison.OrdinalIgnoreCase) Then
            plan.Steps.Add(New InstallStep With {
                .Provider = InstallProvider.Npm,
                .Exe = "npm",
                .Args = "install -g typescript",
                .RequiresAdmin = False
            })
            plan.EnsurePathDirs.Add("%APPDATA%\npm")
            Return plan
        End If

        If spec.Name.Equals("Vite (global)", StringComparison.OrdinalIgnoreCase) Then
            plan.Steps.Add(New InstallStep With {
                .Provider = InstallProvider.Npm,
                .Exe = "npm",
                .Args = "install -g vite",
                .RequiresAdmin = False
            })
            plan.EnsurePathDirs.Add("%APPDATA%\npm")
            Return plan
        End If

        ' Default: Winget (with fallback to choco/scoop when available)
        Dim nameWinget As String = Nothing
        Dim choco As String = Nothing
        Dim scoop As String = Nothing
        Dim ensureDirs As New List(Of String)()

        Select Case spec.Name
            Case "Python"
                nameWinget = "Python"
            Case "Py launcher"
                nameWinget = "Python" ' py comes with Python launcher on Windows
            Case "Java"
                nameWinget = "OpenJDK"
            Case "Go"
                nameWinget = "Go"
            Case "Rust (rustc)"
                nameWinget = "Rustup"
                ensureDirs.Add("%USERPROFILE%\.cargo\bin")
            Case "Ruby"
                nameWinget = "Ruby"
            Case "Node.js"
                nameWinget = "NodeJS"
            Case "npm", "npx"
                ' npm/npx are bundled with Node.js; reinstall Node.js if missing/broken
                nameWinget = "NodeJS"
            Case "Scala"
                nameWinget = "Scala"
            Case "Elixir"
                nameWinget = "Elixir"
            Case "OCaml (ocamlc)"
                ' Winget package that provides Diskuv OCaml (includes ocamlc/opam).
                ' Using a stable ID is more reliable than name search.
                nameWinget = "id:Diskuv.OCaml"
            Case "Deno"
                nameWinget = "Deno"
            Case "Bun"
                nameWinget = "Bun"
            Case "R"
                nameWinget = "R Project"
            Case "Julia"
                nameWinget = "Julia"
            Case "SQLite"
                nameWinget = "SQLite"
            Case "PHP"
                ' Prefer Scoop/Chocolatey. Winget can be ambiguous because msstore apps match "PHP".
                nameWinget = "id:PHP.PHP"
                choco = "php"
                scoop = "php"
            Case "Clojure (clj)"
                ' Winget rarely has a reliable Clojure CLI package; Scoop does.
                scoop = "clojure"
                choco = "clojure"
            Case "NASM"
                nameWinget = "NASM"
            Case "Nim"
                nameWinget = "Nim"
            Case "Crystal"
                nameWinget = "Crystal"
            Case "Bash"
                nameWinget = "Git" ' Git Bash provides bash.exe
            Case Else
                ' If we don't know a safe installer, return empty.
                Return plan
        End Select

        For Each d In ensureDirs
            plan.EnsurePathDirs.Add(d)
        Next

        ' Add multiple provider steps; runner will pick the first that is available.
        ' We prefer user-scoped providers first (Scoop), then Chocolatey, then Winget.
        If Not String.IsNullOrWhiteSpace(scoop) Then
            plan.Steps.Add(New InstallStep With {
                .Provider = InstallProvider.Scoop,
                .Exe = "scoop",
                .Args = "install " & scoop,
                .RequiresAdmin = False
            })
            plan.EnsurePathDirs.Add("%USERPROFILE%\scoop\shims")
        End If

        If Not String.IsNullOrWhiteSpace(choco) Then
            plan.Steps.Add(New InstallStep With {
                .Provider = InstallProvider.Chocolatey,
                .Exe = "choco",
                .Args = "install " & choco & " -y",
                .RequiresAdmin = True
            })
        End If

        If Not String.IsNullOrWhiteSpace(nameWinget) Then
            plan.Steps.Add(New InstallStep With {
                .Provider = InstallProvider.Winget,
                .Exe = "winget",
                .Args = BuildWingetArgs(nameWinget),
                .RequiresAdmin = False
            })
        End If

        Return plan
    End Function

    Private Shared Function BuildWingetArgs(queryNameOrId As String) As String
        ' Force the community "winget" source (avoids msstore false matches), and prefer exact installs.
        Dim q As String = queryNameOrId.Replace("""", "").Trim()
        If q.StartsWith("id:", StringComparison.OrdinalIgnoreCase) Then
            Dim id As String = q.Substring(3).Trim()
            Return "install -e --id """ & id & """ --source winget --accept-package-agreements --accept-source-agreements"
        End If

        Return "install --name """ & q & """ --source winget --accept-package-agreements --accept-source-agreements"
    End Function

    Public Shared Function BuildHumanHint(spec As ToolSpec) As String
        Dim plan As InstallPlan = GetPlan(spec)
        If plan Is Nothing OrElse plan.IsEmpty() Then
            Return "(No automatic installer mapping for this tool.)"
        End If

        Dim lines As New List(Of String)()
        lines.Add("Suggested install commands (auto picks provider):")
        For Each s In plan.Steps
            lines.Add($"- [{s.Provider}] {s.Exe} {s.Args}".Trim())
        Next

        If plan.EnsurePathDirs.Count > 0 Then
            lines.Add("PATH will be ensured for:")
            For Each d In plan.EnsurePathDirs
                lines.Add("- " & d)
            Next
        End If

        Return String.Join(Environment.NewLine, lines)
    End Function

End Class
