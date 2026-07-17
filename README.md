# Pont de Bascule - Application de Gestion des Pesées

Application Windows WPF pour la gestion des pesées de camions avec intégration SAP.

## 🎯 Fonctionnalités

- ✅ Pesée automatique des camions (entrée/sortie)
- ✅ Communication avec balance via port série (RS-232/USB)
- ✅ Intégration SAP via SAP .NET Connector
- ✅ Base de données locale SQLite
- ✅ Interface graphique moderne (WPF)
- ✅ Impression de tickets de pesée
- ✅ Historique des pesées

## 🛠️ Stack Technique

- **Framework**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM avec CommunityToolkit.Mvvm
- **Base de données**: SQLite
- **Communication série**: System.IO.Ports
- **SAP**: SAP .NET Connector 3.0 (NCo)

## 📋 Prérequis

### Pour le développement
- .NET 8.0 SDK ou supérieur
- Visual Studio 2022 ou JetBrains Rider
- Windows 10/11 (pour exécuter l'application)

### Pour SAP
- SAP .NET Connector 3.0 (NCo) - [Télécharger](https://support.sap.com/en/product/connectors/msnet.html)
- Accès SAP avec RFC activé

##  Installation

### 1. Cloner le projet
```bash
git clone https://github.com/Anouar-Elkhalfi/pont_bascule.git
cd pont_bascule
```

### 2. Restaurer les dépendances
```bash
dotnet restore
```

### 3. Configuration

Modifier `appsettings.json` avec vos paramètres :

```json
{
  "Scale": {
    "PortName": "COM1",
    "BaudRate": 9600
  },
  "SAP": {
    "Host": "votre-serveur-sap",
    "SystemNumber": "00",
    "Client": "100",
    "Username": "votre-user",
    "Password": "votre-password"
  }
}
```

### 4. Compiler le projet

**Sur Windows:**
```bash
dotnet build
```

**Depuis Mac/Linux (développement seulement):**
```bash
dotnet build
# Note: L'application ne peut s'exécuter que sur Windows
```

### 5. Exécuter l'application

```bash
dotnet run
```

##  Structure du Projet

```
pont_bascule/
├── Models/              # Modèles de données
│   ├── Weighing.cs
│   ├── ScaleConfiguration.cs
│   └── SapConfiguration.cs
├── Services/            # Services métier
│   ├── IScaleService.cs
│   ├── ScaleService.cs
│   ├── ISapService.cs
│   ├── SapService.cs
│   ├── IDatabaseService.cs
│   └── DatabaseService.cs
├── ViewModels/          # ViewModels MVVM
│   └── MainViewModel.cs
├── Views/               # Vues WPF
│   └── MainWindow.xaml
├── App.xaml             # Application WPF
└── appsettings.json     # Configuration
```

## 🔧 Configuration de la Balance

L'application supporte les balances communiquant via RS-232/USB avec protocole série standard.

**Paramètres typiques :**
- Port: COM1-COM9
- Baud Rate: 9600
- Data Bits: 8
- Parity: None
- Stop Bits: 1

##  Intégration SAP

### Installation SAP NCo

1. Télécharger SAP .NET Connector depuis le [SAP Service Marketplace](https://support.sap.com/en/product/connectors/msnet.html)
2. Installer le package correspondant à votre architecture (x64/x86)
3. Ajouter la référence au projet :
   ```bash
   dotnet add package SAP.Middleware.Connector
   ```

### Configuration RFC

Créer une fonction RFC personnalisée dans SAP pour recevoir les données de pesée :
- Function Module: `Z_CREATE_WEIGHING`
- Paramètres d'import: Numéro camion, Poids, Type de pesée
- Paramètre d'export: Numéro de document SAP

##  Développement sur Mac/Linux

Le développement de base (models, services, logique métier) peut être fait sur Mac/Linux, mais :

- ⚠️ L'interface WPF ne peut pas être visualisée
- ⚠️ L'application ne peut pas s'exécuter
- ✅ Le code peut être édité et compilé
- ✅ Les tests unitaires peuvent être exécutés

**Pour tester l'interface, utilisez un VM Windows ou un PC Windows.**

##  Compilation pour Production

```bash
# Publication pour Windows x64
dotnet publish -c Release -r win-x64 --self-contained

# Le résultat sera dans : bin/Release/net8.0-windows/win-x64/publish/
```

##  Dépannage

### La balance ne se connecte pas
- Vérifier que le port COM est correct
- Vérifier les paramètres de communication (baud rate, etc.)
- Tester le port avec un terminal série

### SAP ne se connecte pas
- Vérifier les credentials SAP
- Vérifier que RFC est activé
- Vérifier la connectivité réseau vers le serveur SAP

##  Contribution

1. Fork le projet
2. Créer une branche (`git checkout -b feature/AmazingFeature`)
3. Commit vos changements (`git commit -m 'Add AmazingFeature'`)
4. Push vers la branche (`git push origin feature/AmazingFeature`)
5. Ouvrir une Pull Request

##  À Implémenter

- [ ] Intégration réelle SAP NCo (actuellement simulé)
- [ ] Protocole de communication série spécifique à votre balance
- [ ] Impression de tickets PDF
- [ ] Export Excel des pesées
- [ ] Authentification utilisateurs
- [ ] Logs détaillés
- [ ] Tests unitaires
- [ ] Interface de configuration graphique

## 📄 Licence

Ce projet est sous licence MIT.

##  Auteurs

- Anouar El Khalfi - Développement initial

##  Support

Pour toute question ou problème, ouvrez une issue sur GitHub.
