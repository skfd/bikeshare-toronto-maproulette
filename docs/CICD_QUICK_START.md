# CI/CD Quick Start Guide

## 🚀 5-Minute Setup

Get your CI/CD pipeline running in 5 minutes with these essential steps.

## Step 1: Push Workflows to GitHub

```bash
# If not already committed
git add .github/
git commit -m "ci: Add GitHub Actions workflows"
git push origin main
```

✅ **Result:** Workflows are now active in your repository

## Step 2: Configure Repository Settings

1. Go to your repository on GitHub
2. Navigate to **Settings** → **Actions** → **General**
3. Set **Workflow permissions** to **"Read and write permissions"**
4. Check **"Allow GitHub Actions to create and approve pull requests"**
5. Click **Save**

✅ **Result:** Workflows can now create releases and update PRs

## Step 3: Update Dependabot Configuration

Edit `.github/dependabot.yml` and replace the placeholder:

```yaml
reviewers:
  - "@your-actual-github-username"  # Replace this!
```

Then commit:
```bash
git add .github/dependabot.yml
git commit -m "ci: Configure Dependabot reviewer"
git push
```

✅ **Result:** Automated dependency updates configured

## Step 4: Verify Everything Works

### Test the CI Pipeline
```bash
# Create a test branch
git checkout -b test/ci-pipeline
echo "# CI Test" > CI_TEST.md
git add CI_TEST.md
git commit -m "test: Verify CI pipeline"
git push -u origin test/ci-pipeline
```

Then:
1. Go to GitHub and create a Pull Request
2. Watch the checks run (takes ~5-10 minutes)
3. Review the automated PR comment with results

✅ **Result:** CI pipeline validated

### Check Workflow Status
Go to the **Actions** tab in your repository to see:
- ✅ Green checkmarks = Success
- 🟡 Yellow dots = In progress
- ❌ Red X = Failed (click to see why)

## Step 5: Your First Release (Optional)

When ready to create a release:

```bash
# Tag your release
git tag v0.1.0
git push origin v0.1.0
```

The release workflow will automatically:
- Build binaries for all platforms
- Create GitHub release
- Generate release notes
- Upload artifacts

✅ **Result:** Version v0.1.0 available on Releases page

## 🎯 Essential Daily Commands

### Check CI Status
```bash
# Install GitHub CLI first (one-time)
winget install GitHub.cli     # Windows
brew install gh                # Mac
sudo apt install gh            # Linux

# Login (one-time)
gh auth login

# View recent workflow runs
gh run list --limit 5

# Watch a running workflow
gh run watch
```

### Fix Common Issues

#### ❌ Build Failed
```bash
# See what failed
gh run view <run-id> --log

# Fix locally first
dotnet build
dotnet test

# Push fix
git add .
git commit -m "fix: Resolve build issue"
git push
```

#### ❌ Security Alert
1. Go to **Security** tab
2. Review the alert
3. Update the package:
```bash
dotnet add package <PackageName> --version <NewVersion>
git add *.csproj
git commit -m "security: Fix vulnerability"
git push
```

## 📊 Status Badges

Add these to your README.md to show CI status:

```markdown
![Build](https://github.com/YOUR_USERNAME/bikeshare-sync/workflows/CI%20Build%20and%20Test/badge.svg)
![Security](https://github.com/YOUR_USERNAME/bikeshare-sync/workflows/Security%20Scanning/badge.svg)
```

## 🔒 Optional: Add Secrets for Enhanced Features

Go to **Settings** → **Secrets and variables** → **Actions**:

| Secret | Purpose | How to Get |
|--------|---------|------------|
| `CODECOV_TOKEN` | Code coverage reports | Sign up at [codecov.io](https://codecov.io) |

## ✅ Success Checklist

- [ ] Workflows pushed to GitHub
- [ ] Repository permissions configured
- [ ] Dependabot reviewer updated
- [ ] Test PR created and passing
- [ ] Actions tab showing green workflows
- [ ] (Optional) First release created

## 🆘 Need Help?

- **Workflow failed?** Click on it in Actions tab for logs
- **Permission denied?** Check Settings → Actions → General
- **Not triggering?** Verify branch names match workflow triggers
- **Full guide:** See [CICD_OPERATIONS_GUIDE.md](./CICD_OPERATIONS_GUIDE.md)

## 📈 Next Steps

Now that CI/CD is running:

1. **Every push** to main/develop triggers builds
2. **Every PR** gets automated testing and feedback
3. **Every Monday** dependencies are checked
4. **Every tag** creates a release

### Pro Tips

- 🏷️ Use semantic versioning: `v1.0.0` format
- 📝 Write clear commit messages: `feat:`, `fix:`, `docs:`
- 🔄 Keep PRs small and focused
- ✅ Don't merge if checks are failing
- 🔐 Never commit secrets or API keys

---

**Time to first green build: ~5 minutes** ⏱️

**Questions?** Create an issue in your repository!