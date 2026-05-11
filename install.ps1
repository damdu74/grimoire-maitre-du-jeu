# ============================================================
#  install.ps1 — Installe Le Grimoire du Maître du Jeu
#                comme tâche planifiée (sans popup UAC)
#
#  À exécuter UNE SEULE FOIS en tant qu'Administrateur.
#  Après ça, GameTrainer.exe se lance toujours en admin,
#  sans popup UAC, via le raccourci créé sur le bureau.
#
#  COMMENT L'EXÉCUTER :
#  Clic droit sur install.ps1 → "Exécuter avec PowerShell"
#  (Si demande de confirmation : taper O puis Entrée)
# ============================================================

$ErrorActionPreference = "Stop"

# ── Vérifie qu'on est bien admin ─────────────────────────────────────────────
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Ce script doit etre lance en Administrateur." -ForegroundColor Red
    Write-Host "Clic droit → 'Executer en tant qu'administrateur'" -ForegroundColor Yellow
    pause
    exit 1
}

# ── Chemins ───────────────────────────────────────────────────────────────────
$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath    = Join-Path $scriptDir "GrimoireMaitreDuJeu.exe"
$taskName   = "GrimoireMaitreDuJeu_NoUAC"
$shortcutPath = [System.IO.Path]::Combine(
    [Environment]::GetFolderPath("Desktop"), "Le Grimoire du Maître du Jeu.lnk")

# Vérifie que l'exe est présent
if (-not (Test-Path $exePath)) {
    Write-Host ""
    Write-Host "ERREUR : GrimoireMaitreDuJeu.exe introuvable dans : $scriptDir" -ForegroundColor Red
    Write-Host "Compile d'abord le projet avec : dotnet publish -c Release -r win-x64 --self-contained true" -ForegroundColor Yellow
    pause
    exit 1
}

Write-Host ""
Write-Host "  Installation du Grimoire du Maître du Jeu..." -ForegroundColor Cyan
Write-Host "  Exe : $exePath" -ForegroundColor Gray

# ── Supprime l'ancienne tâche si elle existe ──────────────────────────────────
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "  Ancienne tache supprimee." -ForegroundColor Gray
}

# ── Crée la tâche planifiée ───────────────────────────────────────────────────
#
#  EXPLICATION DES OPTIONS CLÉS :
#
#  RunLevel = "Highest"
#    → Lance le processus avec les droits admin complets
#      C'est ce qui remplace le manifest requireAdministrator
#      SANS déclencher UAC
#
#  LogonType = "Interactive"
#    → La tâche tourne dans la session de l'utilisateur connecté
#      (nécessaire pour avoir une fenêtre visible)
#
#  AllowStartIfOnBatteries + DontStopIfGoingOnBatteries
#    → Fonctionne sur laptop sans secteur
#
$action   = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $scriptDir
$trigger  = New-ScheduledTaskTrigger -AtLogOn   # Peut aussi être déclenché manuellement
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0)  # Pas de limite de temps

$principal = New-ScheduledTaskPrincipal `
    -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) `
    -RunLevel Highest `
    -LogonType Interactive

# Enregistre la tâche (sans trigger automatique, uniquement à la demande)
Register-ScheduledTask `
    -TaskName  $taskName `
    -Action    $action `
    -Settings  $settings `
    -Principal $principal `
    -Force | Out-Null

# Désactive le déclenchement automatique au logon (on veut lancer manuellement)
$task = Get-ScheduledTask -TaskName $taskName
$task.Triggers = @()  # Aucun trigger automatique
Set-ScheduledTask -InputObject $task | Out-Null

Write-Host "  Tache planifiee creee : '$taskName'" -ForegroundColor Green

# ── Crée un raccourci sur le bureau ───────────────────────────────────────────
#
#  Le raccourci ne pointe PAS vers GameTrainer.exe directement
#  (ce qui déclencherait UAC), mais vers une commande qui
#  lance la tâche planifiée → pas de popup !
#
$wshShell  = New-Object -ComObject WScript.Shell
$shortcut  = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath       = "C:\Windows\System32\schtasks.exe"
$shortcut.Arguments        = "/run /tn `"$taskName`""
$shortcut.WindowStyle      = 7           # Fenêtre réduite (cache le terminal schtasks)
$shortcut.IconLocation     = "$exePath,0" # Icône de l'exe
$shortcut.Description      = "Lance Le Grimoire du Maître du Jeu sans popup UAC"
$shortcut.WorkingDirectory = $scriptDir
$shortcut.Save()

Write-Host "  Raccourci cree sur le bureau : Le Grimoire du Maître du Jeu.lnk" -ForegroundColor Green

# ── Test : lance immédiatement ────────────────────────────────────────────────
Write-Host ""
Write-Host "  Test : lancement du Grimoire..." -ForegroundColor Cyan
Start-ScheduledTask -TaskName $taskName

Start-Sleep -Seconds 1

$taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
if ($taskInfo.LastTaskResult -eq 0 -or $taskInfo.LastRunTime -gt (Get-Date).AddSeconds(-5)) {
    Write-Host "  SUCCESS ! Le Grimoire tourne en arriere-plan." -ForegroundColor Green
} else {
    Write-Host "  Note : Resultat tache = $($taskInfo.LastTaskResult)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "  Le Grimoire du Maitre du Jeu installe !" -ForegroundColor Green
Write-Host ""
Write-Host "  Utilisation future :" -ForegroundColor White
Write-Host "  → Double-clic sur le bureau : 'Le Grimoire du Maitre du Jeu'" -ForegroundColor White
Write-Host "  → Ou : schtasks /run /tn GrimoireMaitreDuJeu_NoUAC" -ForegroundColor Gray
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""
pause
