import type { CapacitorConfig } from '@capacitor/cli'

// The Android app is this web app in a native shell (see ANDROID.md).
//
// RENAMING: the name and the id below are placeholders, and this file is where they are decided.
//  - appName: the name under the icon. After changing it, also change `app_name` and `title_activity_main` in
//    android/app/src/main/res/values/strings.xml (the Android project keeps its own copy).
//  - appId: the package id. It must be final before any store release: Android treats a new id as a different app,
//    so an installed copy can't be updated across the change. It is repeated in android/app/build.gradle
//    (`namespace`, `applicationId`), strings.xml and the folder of MainActivity.java.
const config: CapacitorConfig = {
  appId: 'com.ahmet.canliskor',
  appName: 'Canlı Skor',
  // Vite's build output.
  webDir: 'dist',
}

export default config
