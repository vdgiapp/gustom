
# Gustom
[![UPM branch](https://img.shields.io/badge/github-upm-blue?logo=github)](https://github.com/vdgiapp/gustom/tree/upm)
[![GitHub release](https://img.shields.io/github/v/release/vdgiapp/gustom)](https://github.com/vdgiapp/gustom/releases)
[![](https://img.shields.io/github/release-date/vdgiapp/gustom.svg)](https://github.com/vdgiapp/gustom/releases)
<br/>
![](https://img.shields.io/badge/Unity-2021.2+-57b9d3.svg?style=flat&logo=unity)  
> 🛠️ A collection of reusable tools and utilities for Unity.


## Features
- Scene Selector
- Selection History

## Installation
_This package requires **Unity 2021.3 or later**._

### Install via UPM (with Package Manager UI)

You can add the ArtificeToolkit to your Unity project as a package through Git. Follow these steps:
- Navigate to `Window -> Package Manager` in Unity.
- Click the **"+"** icon in the top-left corner of thePackage Manager.
- Select **"Add package from Git URL..."**.
<p align="center">
  <img src="./Documentation/addpackage.png" />
</p>

- In the popup that appears, paste the following URL: ```https://github.com/vdgiapp/gustom.git#upm```
- To update the package, change suffix `#{version}` to the target version.
    - e.g. `https://github.com/vdgiapp/gustom.git#v1.0.1`


### Install via UPM (Manually)

- Open the `Packages/manifest.json` file in your project. Then add this package somewhere in the `dependencies` block:
  ```json
  {
    "dependencies": {
      "me.giapp.gustom": "https://github.com/vdgiapp/gustom.git#upm",
      ...
    }
  }
  ```

- To update the package, change suffix `#{version}` to the target version.
    - e.g. `"me.giapp.gustom": "https://github.com/vdgiapp/gustom.git#v1.0.1",`
