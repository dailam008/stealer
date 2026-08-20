# ========================================
# FILELESS MALWARE EXECUTION
# ========================================

# Buat folder C:\Stealer kalo belum ada
$logDir = "C:\Stealer"
if (!(Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

# Isi log
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
========================================
"@

# Tulis log ke file
$logContent | Out-File -FilePath $logFile -Encoding utf8
