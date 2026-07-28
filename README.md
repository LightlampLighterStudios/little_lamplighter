# Little Lamplighter

## Before Working on the Project

This project uses **Git LFS** for large files such as images, audio, fonts, models, and Unity packages.

### GitHub Desktop Setup

1. Install [GitHub Desktop](https://desktop.github.com/).
2. Open GitHub Desktop and select **File â†’ Clone repository**.
3. Select the **URL** tab and enter:

   `https://github.com/lucasfgp/little_lamplighter.git`

4. Choose where to save the project, then click **Clone**.

GitHub Desktop supports Git LFS and should automatically download the project's large files. If any files are missing or appear as placeholders, select **Repository -> Pull**.

## Branching Workflow

All new work must start from the `develop` branch.

1. Switch to the `develop` branch in GitHub Desktop.
2. Pull the latest changes using **Repository -> Pull**.
3. Create a new branch from `develop`:
   - Use `feat/<short-description>` for new features.
   - Use `fix/<short-description>` for bug fixes.
4. Make and commit your changes on the new branch.
5. Push the branch to GitHub.
6. Open a pull request to merge your branch back into `develop`.
7. After review and approval, merge the pull request into `develop`.

Do not commit directly to the `main` or `develop` branches.

## Opening the Scene

When you first open the project in Unity, you may see an empty scene. Open:

`Assets/Scenes/LittleLamplighter.unity`
