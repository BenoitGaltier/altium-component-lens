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

Tous les groupes de doublons sont affichés sur une seule page, sans bouton Suivant. Les URL, tokens, langue et dernière comparaison sont sauvegardés automatiquement dans `%LOCALAPPDATA%\ComponentLens\state.dat`, chiffré par Windows pour votre compte. Au redémarrage, les connexions et la comparaison datée sont restaurées sans appel à Altium. Relancez l'analyse pour actualiser les données. Après une mise à jour depuis 1.0.x, saisissez les tokens une première fois ; ils n'étaient pas enregistrés dans les anciennes versions.

## Règles de comparaison

Les références des Part Choices et les paramètres fabricant/MPN sont pris en compte, y compris les paires numérotées. Seule la dernière révision de chaque composant est comparée. La normalisation ignore la casse et les espaces extérieurs et homogénéise les espaces du fabricant ; elle conserve la ponctuation et les espaces intérieurs des MPN. Désactivez-la pour comparer les valeurs brutes exactes. Les alias de fabricants et les équivalences électriques ne sont pas fusionnés automatiquement.

Les filtres utilisent les catégories réelles Altium et leurs chemins complets. Un groupe reste visible si au moins un de ses composants appartient à une catégorie choisie, même si l'autre workspace l'a classé différemment. Les filtres s'appliquent après la lecture complète ; ils ne réduisent donc pas les appels API du scan. Les noms des catégories restent ceux définis dans les workspaces.

L'onglet **Entre A et B** présente les doublons communs. **Dans A** et **Dans B** présentent les doublons internes, y compris ceux également présents dans l'autre workspace. **Tous** réunit ces groupes. Les compteurs de synthèse portent sur l'ensemble de l'analyse ; la table et les exports suivent les filtres.

Les identités manquantes ou non exploitables sont signalées. Un échec de lecture des paramètres produit un rapport **PARTIEL**, qui peut manquer des doublons. Un échec réseau, d'authentification ou de curseur bloque le rapport. La couverture dépend des droits du token et les deux bibliothèques ne constituent pas un instantané atomique : évitez de les modifier pendant l'analyse.

La pagination API suit les curseurs jusqu'à la dernière page. Les lignes répétées sont dédoublonnées par identifiant. Quand le nombre de composants distincts dépasse un total API stable, une seconde lecture complète vérifie les identifiants et les métadonnées. Deux lectures identiques transforment cet écart en information, sans faux avertissement de rapport partiel. Un inventaire incomplet ou changeant, une vérification en échec ou des paramètres non lus restent signalés comme **PARTIEL**, avec un seul bandeau. Les détails sont conservés dans le diagnostic et le JSON. Les curseurs absents, répétés ou sans progression restent bloquants.

Les tokens enregistrés ne sont jamais renvoyés au navigateur et ne figurent ni dans les exports ni dans les journaux. Laissez le champ token vide pour réutiliser celui enregistré pour la même URL. Un changement d'URL nécessite un nouveau token et invalide l'ancienne comparaison ; un renouvellement du token pour la même URL conserve la comparaison. Un rapport partiel reste partiel après restauration. La démonstration n'est pas enregistrée comme résultat réel.

Le menu **Données locales** affiche le dossier et permet d'effacer les connexions et la comparaison, y compris la copie de secours chiffrée. Une seule instance peut utiliser ce dossier à la fois. Un fichier altéré ou indéchiffrable est conservé et signalé. L'application écoute uniquement sur `127.0.0.1` et ne modifie aucune donnée Altium.

Le binaire embarque .NET et peut extraire ses fichiers natifs dans le dossier temporaire au premier lancement. La distribution n'est pas signée. Pour compiler et vérifier le projet, consultez le [README principal](../README.md) et les [limites de validation](VALIDATION.md).
