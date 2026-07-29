# Little Lamplighter

## Before Working on the Project

This project uses **Git LFS** for large files such as images, audio, fonts, models, and Unity packages.

### Unity Version

This project is built with Unity **6000.3.10f1**. Install this exact version through Unity Hub before opening the project. Opening it with a different version can trigger an automatic project upgrade, which may break compatibility for the rest of the team.

### GitHub Desktop Setup

1. Install [GitHub Desktop](https://desktop.github.com/).
2. Open GitHub Desktop and select **File → Clone repository**.
3. Select the **URL** tab and enter:

   `https://github.com/lucasfgp/little_lamplighter.git`

4. Choose where to save the project, then click **Clone**.

GitHub Desktop supports Git LFS and should automatically download the project's large files. If any files are missing or appear as placeholders, select **Repository -> Pull**.

## Opening the Project for the First Time

1. Open **Unity Hub**, click **Add**, and select the folder you just cloned.
2. Make sure it opens with Unity **6000.3.10f1** (see above).
3. On the very first open, Unity has to rebuild its internal `Library` cache and reimport every asset from scratch. This can take several minutes, and during this time the **Scene** and **Game** views will look completely empty — that is expected, not a bug or a missing file. Wait for the progress bar in the bottom-right corner of the Editor to finish.
4. Once the import is done, open the main scene manually in the **Project** window:

   `Assets/Scenes/LittleLamplighter.unity`

   Unity does not load it automatically on a fresh clone.

## Branching Workflow

All new work must start from the `develop` branch.

1. Switch to the `develop` branch in GitHub Desktop.
   
   `git checkout develop`
   
2. Pull the latest changes using **Repository -> Pull**.
   
   `git pull origin develop`
   
3. Create a new branch from `develop`:
   
   - Use `feat/<short-description>` for new features.
     
   - Use `fix/<short-description>` for bug fixes.
     
   `git checkout -b feat/<short-description>`

4. Make and commit your changes on the new branch.
   
   `git add .`
   
   `git commit -m "Describe your change"`
   
5. Push the branch to GitHub.
   
   `git push -u origin feat/<short-description>`

6. Open a pull request to merge your branch back into `develop`.
   In GitHub Desktop: click Create Pull Request (opens the browser).
   Or via CLI (requires GitHub CLI): gh pr create --base develop
7. After review and approval, merge the pull request into `develop`.

Do not commit directly to the `main` or `develop` branches.
