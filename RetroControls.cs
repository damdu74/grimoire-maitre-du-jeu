# 📖 Le Grimoire du Maître du Jeu

Trainer de jeu personnel en C# — lecture et modification mémoire.
Inspiré de Wand / Cheat Engine, pour usage solo uniquement.

---

## 📦 Prérequis

| Outil | Version | Télécharger |
|-------|---------|-------------|
| Windows | 10 ou 11 | — |
| .NET SDK | 8.0 | https://dot.net/download |
| Visual Studio (optionnel) | 2022 Community | https://visualstudio.microsoft.com/fr/ |

---

## 🔨 Compilation et lancement

### Option A — Ligne de commande (simple)
```bash
# 1. Ouvre un terminal dans le dossier GameTrainer/
cd GameTrainer

# 2. Compile + lance en mode debug
dotnet run

# 3. Ou compile en Release (exe standalone)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# L'exe se trouve dans : bin/Release/net8.0-windows/win-x64/publish/GrimoireMaitreDuJeu.exe
```

### Option B — Visual Studio 2022
1. Ouvre `GameTrainer.csproj` dans Visual Studio
2. Clique ▶ Démarrer (ou F5)

### ⚠️ IMPORTANT : Lance TOUJOURS en Administrateur
Le trainer doit être admin pour lire/écrire la mémoire des jeux.
- Clic droit sur `GameTrainer.exe` → "Exécuter en tant qu'administrateur"
- Ou dans Visual Studio : Projet → Propriétés → cocher "Require administrator"

---

## 🕹️ Comment utiliser le trainer

### Étape 1 — Attacher au jeu
1. Lance ton jeu
2. Dans le trainer, clique **"🎮 Choisir le jeu"**
3. Trouve ton jeu dans la liste (filtre par nom)
4. Double-clique ou clique **"✅ Attacher"**

### Étape 2 — Trouver une adresse (exemple : vie = 100)
1. Dans le panneau gauche, sélectionne le **Type** : `Int32 (entier)`
2. **Type de scan** : `Valeur exacte`
3. **Valeur** : tape `100` (ta vie actuelle)
4. Clique **"🔎 Premier scan"** → attends la fin
5. Dans le jeu, **perds de la vie** (ex: tu passes à 75)
6. Change la valeur à `75`, clique **"⏩ Scan suivant"**
7. Répète jusqu'à avoir peu de résultats (idéalement 1-5)
8. Sélectionne l'adresse trouvée, clique **"➕ Ajouter"**

### Étape 3 — Geler la valeur (vie infinie)
1. Dans le panneau droit, ta variable est listée
2. **Double-clique** dessus pour modifier la valeur (ex: mettre 9999)
3. **Coche la case** à gauche OU clique **"❄️ Geler"**
4. La valeur est maintenant écrite en boucle toutes les 100ms → vie infinie !

---

## 📁 Structure du code — Explication

```
GameTrainer/
│
├── Program.cs              ← Point d'entrée. Lance la fenêtre principale.
│
├── Core/
│   ├── ProcessManager.cs   ← ÉTAPE 1 : Trouve les processus Windows,
│   │                          s'y attache avec OpenProcess() (API Win32)
│   │
│   ├── MemoryManager.cs    ← ÉTAPE 2 : Lit et écrit en mémoire avec
│   │                          ReadProcessMemory / WriteProcessMemory.
│   │                          Gère aussi les pointer chains.
│   │
│   └── MemoryScanner.cs    ← ÉTAPE 3 : Scanner à la Cheat Engine.
│                              - Premier scan : parcourt toute la RAM
│                              - Scans suivants : filtre les résultats
│                              - Types : ExactValue, Changed, etc.
│
├── Models/
│   ├── ScanResult.cs       ← Résultat d'un scan (adresse + valeur)
│   └── SavedAddress.cs     ← Adresse sauvegardée avec option de freeze
│
└── UI/
    ├── MainForm.cs          ← ÉTAPE 5 : Fenêtre principale. Orchestre tout.
    │                          - Timer freeze (100ms) : réécrit les valeurs gelées
    │                          - Timer update (500ms) : rafraîchit l'affichage
    │
    └── ProcessSelectorForm.cs ← Fenêtre de sélection du processus cible
```

---

## 🧠 Concepts clés expliqués

### Qu'est-ce qu'une adresse mémoire ?
Chaque variable de ton jeu (vie, argent...) est stockée à une **adresse** dans la RAM.
C'est comme une case dans un tableau géant. L'adresse, c'est le numéro de la case.

### Pourquoi les adresses changent à chaque démarrage ?
Windows utilise **ASLR** (Address Space Layout Randomization) : les adresses changent
à chaque lancement pour des raisons de sécurité. C'est pourquoi on doit **scanner**.

### C'est quoi un pointer chain ?
Les jeux modernes stockent les objets dynamiquement. La vie du joueur n'est pas à une
adresse fixe, mais accessible via une **chaîne** :
```
[ModuleBase + 0x1234] → Pointeur → [+0x58] → Pointeur → [+0x14] = Vie
```
Outils comme Cheat Engine's "Pointer Scanner" permettent de les trouver.

### Comment fonctionne le Freeze ?
Un `System.Windows.Forms.Timer` se déclenche toutes les **100ms**.
Il parcourt toutes les adresses marquées "gelées" et **réécrit** la valeur.
Le jeu réduit la vie → 100ms après, le trainer la remet → effet continu.

---

## ⚠️ Utilisation responsable

✅ **Autorisé** : Jeux solo, pour ton usage personnel  
❌ **Interdit** : Jeux multijoueur (ban possible), distribution du logiciel  

Les anti-cheats (EasyAntiCheat, BattlEye, VAC) détectent ces techniques
sur les jeux en ligne. Utilise **uniquement en solo**.

---

## 🚀 Extensions possibles

- **Pointer Scanner** : automatiser la recherche de pointer chains stables
- **Hot-reload** : sauvegarder/charger des listes d'adresses par jeu
- **Speed hack** : modifier `timeGetTime` / `QueryPerformanceCounter`
- **Overlay en jeu** : fenêtre transparente superposée avec `WS_EX_LAYERED`
- **Injection DLL** : overlay DirectX/ImGui avancé (nécessite C++)
- **Scripts LUA** : système de scripts pour automatiser des actions
