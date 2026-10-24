# Releasing on macOS: signing and notarization

`pnpm build:product <id>` signs, packs and notarizes the macOS app by itself
once the keychain holds two things. Nothing secret is kept in the repo or in
environment files.

## Once per machine

1. **The certificate.** In Xcode: Settings → Accounts → the personal team
   (IONUT LUCIAN CIUNTUC, `7ZSLJT6J2C`) → Manage Certificates → `+` →
   **Developer ID Application**. It lands in the login keychain. Check:

   ```sh
   security find-identity -v -p codesigning   # a "Developer ID Application: …" line
   ```

   Do not use the Nordic Maskin & Rail team (`W37XKCCUD5`): macOS would show
   the client as Lapin's developer.

2. **The notarization profile.** Make an app-specific password at
   appleid.apple.com (Sign-In and Security → App-Specific Passwords), then
   store it in the keychain. The tool asks for the password itself:

   ```sh
   xcrun notarytool store-credentials lapin-notary --apple-id <your Apple ID> --team-id 7ZSLJT6J2C
   ```

## Each release

```sh
pnpm build:product lapin
```

The build:

1. finds the Developer ID identity (or takes `LAPIN_SIGNING_IDENTITY`);
2. signs the app and the engine inside it, with the hardened runtime and
   `desktop/src-tauri/entitlements.plist`: JIT for .NET, and the native
   libraries the engine unpacks;
3. makes the DMG;
4. checks the signature;
5. sends the DMG to Apple (`notarytool submit --wait`, a few minutes);
6. staples the ticket to the DMG and the app;
7. asks Gatekeeper (`spctl`) whether it accepts the app.

The result is in `build/<id>/bundle/dmg/`.

- Without the certificate, the app is built unsigned, as before.
- Without the profile, it is signed but not notarized.
- Another profile name goes in `LAPIN_NOTARY_PROFILE`.

## When Apple refuses

```sh
xcrun notarytool history --keychain-profile lapin-notary
xcrun notarytool log <submission id> --keychain-profile lapin-notary
```

The log names each file and the reason.
