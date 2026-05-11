; ============================================================
;  INNO SETUP — Le Grimoire du Maître du Jeu
;  
;  UTILISATION :
;  1. Télécharge Inno Setup : https://jrsoftware.org/isdl.php
;  2. Compile d'abord le projet :
;     dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
;  3. Ouvre ce fichier dans Inno Setup
;  4. Menu Build → Compile (ou F9)
;  5. L'installateur est créé dans le dossier Output/
; ============================================================

#define AppName      "Le Grimoire du Maître du Jeu"
#define AppVersion   "1.0.0"
#define AppPublisher "Usage Personnel"
#define AppExeName   "GrimoireMaitreDuJeu.exe"
#define AppURL       "https://github.com/damdu74/grimoire-maitre-du-jeu"

; Chemin vers l'exe compilé (ajuste si nécessaire)
#define SourceDir    "..\GameTrainer\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
; ── Identité ──────────────────────────────────────────────────────────────────
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; ── Dossier d'installation ────────────────────────────────────────────────────
DefaultDirName={autopf}\GrimoireMaitreDuJeu
DefaultGroupName={#AppName}
AllowNoIcons=yes

; ── Fichier de sortie ─────────────────────────────────────────────────────────
OutputDir=Output
OutputBaseFilename=GrimoireInstaller_v{#AppVersion}

; ── Compression ───────────────────────────────────────────────────────────────
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; ── Apparence ─────────────────────────────────────────────────────────────────
WizardStyle=modern
SetupIconFile=grimoire.ico
; Si tu n'as pas d'icône, commente la ligne ci-dessus

; ── Droits ────────────────────────────────────────────────────────────────────
; Demande les droits admin (nécessaire pour créer la tâche planifiée)
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; ── Désinstallateur ───────────────────────────────────────────────────────────
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}

; ── Mise à jour ───────────────────────────────────────────────────────────────
; Permet de ré-installer par-dessus une version existante
CloseApplications=yes
CloseApplicationsFilter=*{#AppExeName}*
RestartApplications=no

[Languages]
; Interface en français
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
french.WelcomeLabel2=Ce programme va installer [name/ver] sur votre ordinateur.%n%nIl est recommandé de fermer toutes les autres applications avant de continuer.%n%nCliquez sur Suivant pour continuer.
french.FinishedHeadingLabel=Installation de [name] terminée
french.FinishedLabelNoIcons=L'installation de [name] est maintenant terminée.

[Tasks]
; Option pour créer un raccourci bureau
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked

[Files]
; ── Copie l'exe principal ─────────────────────────────────────────────────────
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; ── Copie la police Orbitron si elle existe ───────────────────────────────────
Source: "fonts\Orbitron-Bold.ttf";     DestDir: "{app}\fonts"; Flags: ignoreversion skipifsourcedoesntexist
Source: "fonts\Orbitron-Regular.ttf";  DestDir: "{app}\fonts"; Flags: ignoreversion skipifsourcedoesntexist

; ── Copie le script de désinstallation de la tâche planifiée ─────────────────
Source: "..\GameTrainer\uninstall.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Raccourci dans le menu Démarrer
Name: "{group}\{#AppName}";  Filename: "{app}\{#AppExeName}"
Name: "{group}\Désinstaller"; Filename: "{uninstallexe}"

; Raccourci bureau (optionnel)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; ── Crée la tâche planifiée pour lancer sans UAC ─────────────────────────────
; Exécuté à la fin de l'installation
Filename: "powershell.exe";
Parameters: "-ExecutionPolicy Bypass -NonInteractive -Command ""$action = New-ScheduledTaskAction -Execute '{app}\{#AppExeName}'; $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Hours 0); $principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -RunLevel Highest -LogonType Interactive; Register-ScheduledTask -TaskName 'GrimoireMaitreDuJeu_NoUAC' -Action $action -Settings $settings -Principal $principal -Force | Out-Null""";
Flags: runhidden waituntilterminated;
StatusMsg: "Configuration des permissions...";

; ── Lance le Grimoire après installation (optionnel) ─────────────────────────
Filename: "{app}\{#AppExeName}"; Description: "Lancer Le Grimoire du Maître du Jeu"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; ── Supprime la tâche planifiée à la désinstallation ─────────────────────────
Filename: "powershell.exe";
Parameters: "-ExecutionPolicy Bypass -NonInteractive -Command ""Unregister-ScheduledTask -TaskName 'GrimoireMaitreDuJeu_NoUAC' -Confirm:$false -ErrorAction SilentlyContinue""";
Flags: runhidden waituntilterminated;

[Registry]
; Enregistre la version dans le registre (pour AutoUpdater)
Root: HKLM; Subkey: "SOFTWARE\GrimoireMaitreDuJeu"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\GrimoireMaitreDuJeu"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[Code]
// ── Vérification de la version précédente ────────────────────────────────────
// Si une version est déjà installée, on affiche un message de mise à jour
// au lieu du message d'installation standard.

function GetUninstallString: string;
var
  sUnInstPath: string;
  sUnInstallString: string;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

function IsUpgrade: Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

function InitializeSetup: Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Rien de spécial, Inno Setup gère le remplacement automatiquement
  end;
end;
