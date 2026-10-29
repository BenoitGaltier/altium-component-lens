# Component Lens — Français

Application Windows portable pour repérer les doublons de composants entre deux workspaces Altium 365. Le critère est le couple **Manufacturer + Manufacturer Part Number**. L'interface est disponible en français, anglais et espagnol.

## Démarrage

Téléchargez le ZIP `win-x64` dans les Releases du dépôt, décompressez-le puis lancez **ComponentLens.exe**. Le navigateur s'ouvre automatiquement. Aucune installation de .NET, Node.js ou Altium Designer n'est nécessaire. Gardez la fenêtre de l'exécutable ouverte ; **Quitter** ou la fermeture de cette fenêtre arrête l'application.

Prérequis : Windows 10/11 x64, navigateur récent, accès réseau à Altium 365 et un token API autorisant la lecture de la bibliothèque pour chaque workspace.

1. Configurez et connectez les workspaces A et B avec leur URL et leur token.
2. Lancez **Rechercher les doublons**.
3. Sélectionnez les catégories Altium souhaitées : résistances, condensateurs ou tout autre type réellement présent dans les bibliothèques.
4. Développez les références pour voir les composants associés, les catégories, les révisions et la source de l'identité.
5. Exportez les résultats filtrés en CSV ou JSON. La carte **Données manquantes** permet d'inspecter et d'exporter les anomalies.

**Explorer la démo** utilise des données fictives identifiées et ne contacte aucun workspace.

## Règles de comparaison

Les références des Part Choices et les paramètres fabricant/MPN sont pris en compte, y compris les paires numérotées. Seule la dernière révision de chaque composant est comparée. La normalisation ignore la casse et les espaces extérieurs et homogénéise les espaces du fabricant ; elle conserve la ponctuation et les espaces intérieurs des MPN. Désactivez-la pour comparer les valeurs brutes exactes. Les alias de fabricants et les équivalences électriques ne sont pas fusionnés automatiquement.

Les filtres utilisent les catégories réelles Altium et leurs chemins complets. Un groupe reste visible si au moins un de ses composants appartient à une catégorie choisie, même si l'autre workspace l'a classé différemment. Les filtres s'appliquent après la lecture complète ; ils ne réduisent donc pas les appels API du scan. Les noms des catégories restent ceux définis dans les workspaces.

L'onglet **Entre A et B** présente les doublons communs. **Dans A** et **Dans B** présentent les doublons internes, y compris ceux également présents dans l'autre workspace. **Tous** réunit ces groupes. Les compteurs de synthèse portent sur l'ensemble de l'analyse ; la table et les exports suivent les filtres.

Les identités manquantes ou non exploitables sont signalées. Un échec de lecture des paramètres produit un rapport **PARTIEL**, qui peut manquer des doublons. Un échec d'inventaire ou d'authentification bloque le rapport. La couverture dépend des droits du token et les deux bibliothèques ne constituent pas un instantané atomique : évitez de les modifier pendant l'analyse.

Les tokens restent en mémoire et ne figurent ni dans les fichiers exportés, ni dans les journaux, ni dans le stockage du navigateur. Fermez l'exécutable pour les effacer de la session. L'application écoute uniquement sur l'adresse locale `127.0.0.1` et ne modifie aucune donnée Altium. Les noms, URL des workspaces et la langue sont mémorisés dans le navigateur.

Le binaire embarque .NET et peut extraire ses fichiers natifs dans le dossier temporaire au premier lancement. La distribution n'est pas signée. Pour compiler et vérifier le projet, consultez le [README principal](../README.md) et les [limites de validation](VALIDATION.md).
