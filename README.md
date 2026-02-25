<div align="center">

<!-- Animated Wave Header -->
<img src="https://capsule-render.vercel.app/api?type=waving&height=210&color=0:6366f1,100:22c55e&text=DevCheck%20HasV&fontSize=56&fontColor=ffffff&animation=fadeIn&fontAlignY=35&desc=Fast%20PowerShell%20Version%20Checker%20(Windows)&descAlignY=58" />

<!-- Typing SVG -->
<img src="https://readme-typing-svg.demolab.com?font=Fira+Code&size=18&duration=2600&pause=650&color=22C55E&center=true&vCenter=true&width=860&lines=Check+PUNYA%2FTIDAK+PUNYA+%2B+Version;One-line+output+per+tool;Tcl+fixed+(no+interactive+hang)" />

<p>
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D6?logo=windows&logoColor=white" />
  <img src="https://img.shields.io/badge/PowerShell-5.1%2B%20%7C%207+-5391FE?logo=powershell&logoColor=white" />
  <img src="https://img.shields.io/badge/Script-Portable-111827" />
</p>

<p align="center">
  🧰 <b>DevCheck HasV</b> adalah script PowerShell untuk mengecek <b>ketersediaan</b> dan <b>versi</b> berbagai bahasa pemrograman & toolchain di Windows.
</p>

</div>

---

## Ringkasan

Script ini menampilkan output seperti:

- ✅ `PUNYA (version...)`
- ❌ `TIDAK PUNYA`

Setiap tool ditampilkan **1 baris**, jadi gampang dibaca dan cepat dicek.

---

## Kenapa script sebelumnya berhenti di Tcl?

Di beberapa instalasi Windows, perintah seperti:

- `tclsh --version`
- `tclsh -c "..."`

bisa **masuk mode interaktif** (`%`) dan membuat script **hang** (menunggu input).

✅ Solusi yang stabil: ambil versi Tcl dengan **pipe**:

```powershell
'puts [info patchlevel]' | tclsh
```

Karena itu fungsi `HasV` dibuat punya **kasus khusus** untuk `tclsh`.

---

## Cara Pakai

1) Copy script di bawah ke file misalnya: `devcheck.ps1`  
2) Jalankan:

```powershell
powershell -ExecutionPolicy Bypass -File .\devcheck.ps1
```

---

## Script HasV (sudah fix Tcl)

```powershell
function First-NonEmptyLine($lines) {
  foreach ($x in @($lines)) {
    $s = [string]$x
    if ($s -and $s.Trim()) { return $s.Trim() }
  }
  return $null
}

function HasV($name, $cmd, $verArgsText = "--version") {
  $gc = Get-Command $cmd -ErrorAction SilentlyContinue

  # fallback khusus Tcl
  if (-not $gc -and $cmd -ieq "tclsh") {
    $gc = Get-Command tclsh86t -ErrorAction SilentlyContinue
    if ($gc) { $cmd = "tclsh86t" }
  }

  if (-not $gc) {
    "$name : TIDAK PUNYA"
    return
  }

  # ===== Kasus khusus Tcl =====
  if ($cmd -ieq "tclsh" -or $cmd -ieq "tclsh86t") {
    try {
      $exe = if ($gc.Source -and (Test-Path $gc.Source)) { $gc.Source } else { $cmd }

      $tmp = Join-Path $env:TEMP "check_tcl_version.tcl"
      Set-Content -LiteralPath $tmp -Value 'puts [info patchlevel]' -Encoding ASCII -NoNewline

      $out = & $exe $tmp 2>&1
      Remove-Item $tmp -Force -ErrorAction SilentlyContinue

      $line = First-NonEmptyLine $out
      if ($line) {
        "$name : PUNYA ($line)"
      } else {
        "$name : PUNYA (-)"
      }
    } catch {
      "$name : PUNYA (-)"
    }
    return
  }

  # ===== Kasus khusus Clojure alias/function PowerShell =====
  if ($cmd -ieq "clj") {
    try {
      $out = & $cmd --version 2>&1
      $line = First-NonEmptyLine $out

      if (-not $line) {
        $out = & $cmd -Sdescribe 2>&1
        $line = First-NonEmptyLine $out
      }

      if ($line) {
        "$name : PUNYA ($line)"
      } else {
        "$name : PUNYA (-)"
      }
    } catch {
      "$name : TIDAK PUNYA"
    }
    return
  }

  # ===== Default =====
  $out = cmd /c "$cmd $verArgsText 2>&1"
  $line = First-NonEmptyLine $out

  if (-not $line -or $line -match 'is not recognized as an internal or external command') {
    "$name : TIDAK PUNYA"
    return
  }

  # Ambil baris Elixir yang benar
  if ($cmd -ieq "elixir") {
    $elixirLine = @($out | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^Elixir\s' } | Select-Object -First 1)
    if ($elixirLine) {
      $line = $elixirLine[0].Trim()
    }
  }

  "$name : PUNYA ($line)"
}

# General purpose
HasV "Python" "python" "--version"
HasV "Py launcher" "py" "--version"
HasV "C (gcc)" "gcc" "--version"
HasV "C++ (g++)" "g++" "--version"
HasV "Java" "java" "-version"
HasV ".NET" "dotnet" "--version"
HasV "Go" "go" "version"
HasV "Swift" "swift" "--version"
HasV "Kotlin (kotlinc)" "kotlinc" "-version"
HasV "Ruby" "ruby" "--version"
HasV "Rust (rustc)" "rustc" "--version"

# Web & JS
HasV "Node.js" "node" "--version"
HasV "V8 (JS engine)" "node" "-p ""process.versions.v8"""
HasV "npm" "npm" "--version"
HasV "npx" "npx" "--version"
HasV "TypeScript (tsc)" "tsc" "--version"
HasV "Vite (global)" "vite" "--version"
HasV "Deno" "deno" "--version"
HasV "Bun" "bun" "--version"

# Data/Math/Science
HasV "R" "R" "--version"
HasV "Julia" "julia" "--version"
HasV "SQLite" "sqlite3" "--version"

# Functional & Academic
HasV "Haskell (ghc)" "ghc" "--version"
HasV "Scala" "scala" "--version"
HasV "Clojure (clj)" "clj" "-Sdescribe"
HasV "Elixir" "elixir" "--version"
HasV "Erlang (erl)" "erl" "-version"
HasV "OCaml (ocamlc)" "ocamlc" "-version"
HasV "Scheme (Guile)" "guile" "--version"

# Scripting/System
HasV "Bash" "bash" "--version"
HasV "PHP" "php" "--version"
HasV "Lua" "lua" "-v"
HasV "Tcl (tclsh)" "tclsh" "--version"
HasV "Nim" "nim" "--version"
HasV "Crystal" "crystal" "--version"

# Assembler
HasV "NASM" "nasm" "-v"
```

---

## Tips: Menambah / Mengurangi Tool

- Tambah baris baru:
  - `HasV "Nama" "command" "--version"`
- Kalau command versi berbeda:
  - Java: `-version`
  - Go: `version`
  - NASM/Lua: `-v`
- Kalau ada tool yang **lama** start-up (contoh: `R --version`), kamu bisa **comment** barisnya sementara.

---

## Troubleshooting

### 1) Output versi “kosong” atau jadi “-”
Beberapa tool menaruh versi di output yang berbeda atau butuh flag khusus. Coba ganti parameter versi pada baris `HasV`.

### 2) Script terasa lama
Biasanya karena ada tool yang start-up berat. Comment dulu tool tersebut, lalu jalankan lagi.

### 3) Tcl masuk prompt `%`
Pastikan kamu pakai script yang sudah ada **kasus khusus `tclsh`** (bagian pipe).

---

## License

Gunakan bebas untuk kebutuhan pribadi/portofolio. Tambahkan file `LICENSE` jika ingin open-source (MIT/Apache-2.0).
