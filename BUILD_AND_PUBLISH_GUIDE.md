# Build & Publish Guide for Job Order Generator

This guide explains how to use the two automated build and deployment PowerShell scripts in this repository:
1. **`build_local_installer.ps1`** — For local testing on your own computers.
2. **`publish_update.ps1`** — For publishing official live updates to your customers via GitHub and CloudAdmin.

---

## Quick Comparison Summary

| Feature / Action | `build_local_installer.ps1` | `publish_update.ps1` |
| :--- | :---: | :---: |
| **Purpose** | Local building & testing | Official customer release |
| **Generates WPF Single-File EXE** | ✅ Yes | ✅ Yes |
| **Compiles Inno Setup `.exe`** | ✅ Yes | ✅ Yes |
| **Uploads to GitHub Releases** | ❌ No | ✅ Yes |
| **Inserts into CloudAdmin Server** | ❌ No | ✅ Yes (Autofill support) |
| **Internet Connection Required** | ❌ No | ✅ Yes |
| **Risk to Live Customers** | 🛡️ None (Safe for testing) | ⚠️ Pushes live update |

---

## 1. `build_local_installer.ps1` — Local Testing Guide

### What It Does:
* Updates the version string in `ClientApp.csproj` and `JobOrderGenerator.iss`.
* Compiles the WPF application into a self-contained single-file executable (`JobOrderGenerator.exe`).
* Uses Inno Setup (`ISCC.exe`) to build a standalone Windows Installer executable (`JobOrderGenerator_Setup_vX.X.X.exe`).
* **Keeps everything on your local machine** without uploading anything to GitHub or CloudAdmin.

### When to Use It:
Use this script when you want to build and test a new feature or bugfix locally on your shop computers before releasing it to your paying customers.

### How to Use It:

1. Open PowerShell in the root project folder: `f:\Service Memo App`
2. Run the command:
   ```powershell
   .\build_local_installer.ps1 -Version "1.4.0"
   ```
3. Once the build finishes, find your compiled installer at:
   `ClientApp\bin\Release\net10.0-windows\win-x64\publish\JobOrderGenerator_Setup_v1.4.0.exe`
4. Run that setup file directly on any of your local PCs to test the app.

---

## 2. `publish_update.ps1` — Live Customer Release Guide

### What It Does:
* Updates version strings in `ClientApp.csproj` and `JobOrderGenerator.iss`.
* Compiles the WPF single-file executable (`JobOrderGenerator.exe`).
* Compiles the Inno Setup installer executable (`JobOrderGenerator_Setup_vX.X.X.exe`).
* Automatically creates a new **GitHub Release** tag (e.g. `v1.4.0`) on `albertantony308/JobOrderGenerator`.
* Uploads the setup executable asset directly to GitHub Releases.
* Enables one-click **Autofill** in your **CloudAdmin** dashboard so all active customer client apps download and install the update.

### When to Use It:
Use this script when you are ready to publish an official software update to your customers.

### How to Use It:

1. Open PowerShell in the root project folder: `f:\Service Memo App`
2. Run the command:
   ```powershell
   .\publish_update.ps1 -Version "1.4.0" -Changelog "v1.4.0 Event-Driven Realtime Sync, Offline Outbox Queue & Multi-Device Force Sync"
   ```
   *(If prompted, enter your GitHub Personal Access Token).*

3. Open your **CloudAdmin** web dashboard (under the **App Updates** tab).
4. Click **Fetch GitHub Releases**.
5. Click **Autofill Form** next to version `v1.4.0`.
6. Click **Publish Software Update**.

All online client apps will automatically receive the update notification and install **Version 1.4.0**!

---

## Database & Customer Data Safety Note

Both scripts ensure **100% data preservation**. Customer databases (`local_memos.db`) stored in `%LocalAppData%\ServiceMemoApp\` are never overwritten during updates.
