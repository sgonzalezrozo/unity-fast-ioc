# Platform Check

Checks that Fast IoC works on a platform: that it starts at all (its startup assembly survives code stripping),
constructor and field injection, singletons, `Lazy<T>` and `Func<T>`, NonLazy bindings, `FromInstance(value)`, an asset
loaded through the generated asset manifest, exporting bindings to another container, prefab instantiation, and sync
and async initialization. Hosted components (`FromProjectContext`) need your `ProjectContext` prefab, so they aren't checked here.

1. Import this sample from **Window > Package Manager > Fast IoC > Samples**.
2. Let Fast IoC regenerate its code.
3. Add the **Platform Check** component to any object in your first scene.
4. Build and run for the platform you want to check. Try the stripping level you ship with
   (**Player Settings > Other Settings > Managed Stripping Level**).
5. Read the results on screen, or in the log (the browser console on WebGL).

Remove the sample when you're done: its installer adds bindings to your project.
