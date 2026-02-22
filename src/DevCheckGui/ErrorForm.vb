Option Strict On
Option Explicit On

Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Windows.Forms

''' <summary>
''' Fallback window shown when MainForm fails to start.
''' This prevents the app from "doing nothing" and helps users see the real error.
''' </summary>
Public Class ErrorForm
    Inherits Form

    Private ReadOnly _logPath As String
    Private ReadOnly _ex As Exception

    Public Sub New(ex As Exception, logPath As String)
        _ex = If(ex, New Exception("Unknown startup error"))
        _logPath = If(logPath, "")

        Text = "DevCheck — Error"
        StartPosition = FormStartPosition.CenterScreen
        MinimumSize = New Drawing.Size(900, 600)
        Font = New Drawing.Font("Segoe UI", 10.0F)

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .RowCount = 3,
            .ColumnCount = 1,
            .Padding = New Padding(14)
        }
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

        Dim title As New Label() With {
            .Text = "The application failed to start.",
            .AutoSize = True,
            .Font = New Drawing.Font("Segoe UI", 12.0F, Drawing.FontStyle.Bold)
        }

        Dim txt As New TextBox() With {
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Both,
            .Dock = DockStyle.Fill,
            .Font = New Drawing.Font("Consolas", 10.0F)
        }
        txt.Text = _ex.ToString() &
            (If(String.IsNullOrWhiteSpace(_logPath), "", vbCrLf & vbCrLf & "Log: " & _logPath))

        Dim bottom As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.LeftToRight,
            .AutoSize = True,
            .WrapContents = False
        }

        Dim btnCopy As New Button() With {.Text = "Copy"}
        AddHandler btnCopy.Click,
            Sub()
                Try
                    Clipboard.SetText(txt.Text)
                Catch
                End Try
            End Sub

        Dim btnOpenLog As New Button() With {.Text = "Open log folder"}
        AddHandler btnOpenLog.Click,
            Sub()
                Try
                    If Not String.IsNullOrWhiteSpace(_logPath) Then
                        Dim folder As String = Path.GetDirectoryName(_logPath)
                        If Not String.IsNullOrWhiteSpace(folder) Then
                            Process.Start(New ProcessStartInfo() With {
                                .FileName = folder,
                                .UseShellExecute = True
                            })
                        End If
                    End If
                Catch
                End Try
            End Sub

        Dim btnClose As New Button() With {.Text = "Close"}
        AddHandler btnClose.Click, Sub() Close()

        bottom.Controls.Add(btnCopy)
        bottom.Controls.Add(btnOpenLog)
        bottom.Controls.Add(btnClose)

        root.Controls.Add(title, 0, 0)
        root.Controls.Add(txt, 0, 1)
        root.Controls.Add(bottom, 0, 2)

        Controls.Add(root)
    End Sub
End Class
