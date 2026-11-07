# CI project

An empty Unity project that the GitHub Actions workflow (`../workflows/tests.yml`) opens to run the package's
Edit Mode tests. It references this repository with `file:../../..` and lists the package as testable.
Unity ignores this folder when the package is installed, because it starts with a dot.
