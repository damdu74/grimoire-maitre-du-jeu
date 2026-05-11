# ============================================================
#  uninstall.ps1 — Supprime la tâche planifiée
#                  du Grimoire du Maître du Jeu
# ============================================================

$taskName     = "GrimoireMaitreDuJeu_NoUAC"
$shortcutPath = [System.IO.Path]::Combine(
    [Environment]::GetFolderPath("Desktop"), "Le Grimoire du Maître du Jeu.lnk")

if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Tache '$taskName' supprimee." -ForegroundColor Green
} else {
    Write-Host "Tache '$taskName' introuvable." -ForegroundColor Yellow
}

if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
    Write-Host "Raccourci bureau supprime." -ForegroundColor Green
}

Write-Host "Desinstallation du Grimoire terminee." -ForegroundColor Cyan
pause
