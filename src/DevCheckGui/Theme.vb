Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Windows.Forms

Public Enum AppTheme
    Light = 0
    Dark = 1
End Enum

Public NotInheritable Class ThemeManager

    Public Shared Sub ApplyTheme(root As Control, theme As AppTheme)
        Dim pal As ThemePalette = If(theme = AppTheme.Dark, ThemePalette.Dark(), ThemePalette.Light())

        ApplyToControlTree(root, pal)

        ' DataGridView needs a bit more care
        For Each dgv In FindAllDataGrids(root)
            ApplyToGrid(dgv, pal)
        Next
    End Sub

    Private Shared Sub ApplyToControlTree(c As Control, pal As ThemePalette)
        If TypeOf c Is TextBox Then
            Dim tb As TextBox = DirectCast(c, TextBox)
            tb.BackColor = pal.InputBack
            tb.ForeColor = pal.Text
        ElseIf TypeOf c Is DataGridView Then
            ' handled separately
        ElseIf TypeOf c Is Button Then
            Dim b As Button = DirectCast(c, Button)
            b.BackColor = pal.ButtonBack
            b.ForeColor = pal.ButtonText
            b.FlatStyle = FlatStyle.Flat
            b.FlatAppearance.BorderColor = pal.Border
            b.FlatAppearance.BorderSize = 1
            b.Padding = New Padding(10, 6, 10, 6)
        ElseIf TypeOf c Is ComboBox Then
            Dim cb As ComboBox = DirectCast(c, ComboBox)
            cb.BackColor = pal.InputBack
            cb.ForeColor = pal.Text
            cb.FlatStyle = FlatStyle.Flat
        ElseIf TypeOf c Is CheckedListBox Then
            Dim clb As CheckedListBox = DirectCast(c, CheckedListBox)
            clb.BackColor = pal.PanelBack
            clb.ForeColor = pal.Text
        ElseIf TypeOf c Is ProgressBar Then
            ' keep default
        ElseIf TypeOf c Is Label Then
            Dim l As Label = DirectCast(c, Label)
            l.ForeColor = pal.MutedText
        ElseIf TypeOf c Is GroupBox Then
            Dim gb As GroupBox = DirectCast(c, GroupBox)
            gb.ForeColor = pal.Text
            gb.BackColor = pal.PanelBack
        ElseIf TypeOf c Is Panel OrElse TypeOf c Is TableLayoutPanel OrElse TypeOf c Is FlowLayoutPanel Then
            c.BackColor = pal.PanelBack
            c.ForeColor = pal.Text
        ElseIf TypeOf c Is Form Then
            Dim f As Form = DirectCast(c, Form)
            f.BackColor = pal.WindowBack
            f.ForeColor = pal.Text
        Else
            c.BackColor = pal.PanelBack
            c.ForeColor = pal.Text
        End If

        For Each child As Control In c.Controls
            ApplyToControlTree(child, pal)
        Next
    End Sub

    Private Shared Function FindAllDataGrids(root As Control) As DataGridView()
        Dim list As New System.Collections.Generic.List(Of DataGridView)()
        CollectGrids(root, list)
        Return list.ToArray()
    End Function

    Private Shared Sub CollectGrids(c As Control, list As System.Collections.Generic.List(Of DataGridView))
        If TypeOf c Is DataGridView Then
            list.Add(DirectCast(c, DataGridView))
        End If
        For Each child As Control In c.Controls
            CollectGrids(child, list)
        Next
    End Sub

    Private Shared Sub ApplyToGrid(grid As DataGridView, pal As ThemePalette)
        grid.BackgroundColor = pal.PanelBack
        grid.GridColor = pal.Border
        grid.EnableHeadersVisualStyles = False

        grid.ColumnHeadersDefaultCellStyle.BackColor = pal.HeaderBack
        grid.ColumnHeadersDefaultCellStyle.ForeColor = pal.Text
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = pal.HeaderBack
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = pal.Text

        grid.DefaultCellStyle.BackColor = pal.PanelBack
        grid.DefaultCellStyle.ForeColor = pal.Text
        grid.DefaultCellStyle.SelectionBackColor = pal.SelectionBack
        grid.DefaultCellStyle.SelectionForeColor = pal.Text

        grid.RowHeadersVisible = False
        grid.BorderStyle = BorderStyle.FixedSingle
    End Sub

End Class

Public NotInheritable Class ThemePalette
    Public Property WindowBack As Color
    Public Property PanelBack As Color
    Public Property HeaderBack As Color
    Public Property InputBack As Color
    Public Property SelectionBack As Color
    Public Property Text As Color
    Public Property MutedText As Color
    Public Property Border As Color
    Public Property ButtonBack As Color
    Public Property ButtonText As Color

    Public Shared Function Light() As ThemePalette
        Return New ThemePalette With {
            .WindowBack = Color.White,
            .PanelBack = Color.FromArgb(248, 250, 252),
            .HeaderBack = Color.FromArgb(241, 245, 249),
            .InputBack = Color.White,
            .SelectionBack = Color.FromArgb(219, 234, 254),
            .Text = Color.FromArgb(15, 23, 42),
            .MutedText = Color.FromArgb(71, 85, 105),
            .Border = Color.FromArgb(203, 213, 225),
            .ButtonBack = Color.FromArgb(34, 197, 94),   ' green accent
            .ButtonText = Color.White
        }
    End Function

    Public Shared Function Dark() As ThemePalette
        Return New ThemePalette With {
            .WindowBack = Color.FromArgb(15, 23, 42),
            .PanelBack = Color.FromArgb(17, 24, 39),
            .HeaderBack = Color.FromArgb(30, 41, 59),
            .InputBack = Color.FromArgb(30, 41, 59),
            .SelectionBack = Color.FromArgb(59, 130, 246),
            .Text = Color.FromArgb(226, 232, 240),
            .MutedText = Color.FromArgb(148, 163, 184),
            .Border = Color.FromArgb(51, 65, 85),
            .ButtonBack = Color.FromArgb(34, 197, 94),
            .ButtonText = Color.FromArgb(15, 23, 42)
        }
    End Function
End Class
