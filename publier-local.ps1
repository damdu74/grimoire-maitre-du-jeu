# Compile le Grimoire, l'installe dans %LOCALAPPDATA%\Programs\Grimoire
# et (re)crée le raccourci du bureau. À relancer après chaque modification.
# Pas d'admin requis : l'appli tourne avec les droits normaux (asInvoker).

$ErrorActionPreference = 'Stop'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Grimoire'

# Ferme l'appli si elle tourne, sinon l'exe est verrouillé
Get-Process GrimoireMaitreDuJeu -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet publish "$PSScriptRoot\GameTrainer.csproj" -c Release -r win-x64 --self-contained false -o $dest -v q
if ($LASTEXITCODE -ne 0) { throw "Échec de la compilation" }

$exe = Join-Path $dest 'GrimoireMaitreDuJeu.exe'
$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Le Grimoire du Maître du Jeu.lnk'
$sc = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
$sc.TargetPath       = $exe
$sc.WorkingDirectory = $dest
$sc.IconLocation     = "$exe,0"
$sc.Description      = 'Triches pour jeux solo'
$sc.Save()
"Installé dans $dest — raccourci : $lnk"
