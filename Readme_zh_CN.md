# ExplorerEx

### ExplorerEx是一个响应快、多标签页、现代化界面且人性化的文件浏览器。

![Preview](https://github.com/DearVa/ExplorerEx/blob/master/Images/preview.png)

由于不为人知的原因，Windows 11的文件管理器响应十分缓慢，特别是打开文件夹、框选文件、复制粘贴时，在低端机上甚至能够停止响应几秒钟。

前一段我在整理Minecraft Mod时，需要频繁在多个文件夹中切换，不仅响应十分缓慢，而且用户呼声很高的“多标签页”功能至今还没有。于是我就产生了自己开发一个文件管理器的想法。



### 该管理器具有以下的特点：

* **响应快**。我的标准是，中端机型（如i5系列CPU或同等性能AMD CPU）可以在500ms内打开一个文件夹，即便是类似C:\Windows\System32这种有大量文件（我刚看了一下，我这里有4,778个文件）的文件夹依旧要很快打开。

* **多标签页**。支持将文件拖到标签页上复制、移动、创建快捷方式等。你还可以直接拖动标签页来分屏，就像VS Code那样简单。

  ![SplitScreen](https://github.com/DearVa/ExplorerEx/blob/master/Images/SplitScreen.png)

* **超级收藏夹**。Windows自带文件管理器的不仅无法备注、无法添加文件，还只有一个“快捷访问”栏，无法进行分类。ExplorerEx可以直接收藏文件夹甚至是文件！还可添加备注、归类，方便查找，就像浏览器那样！

  ![Explorer不能做到](https://github.com/DearVa/ExplorerEx/blob/master/Images/ExplorerCantAddFile.jpg)

  ![ExplorerEx可以做到](https://github.com/DearVa/ExplorerEx/blob/master/Images/SuperBookmarks.png)

* **现代化界面**。目前使用HandyControl: [欢迎使用HandyControl | HandyOrg](https://handyorg.github.io/handycontrol/)，我会对其进行一定的修改，使其倾向于Windows 11的Fluent UI风格，还会添加一些~~徒增功耗~~好看的动画（在响应快的前提下）。

* **人性化**。对于普通的用户，界面可以简单易懂，但是也有高级功能（如：创建硬链接、修改文件拓展名时不提示），用户可以在设置或者其他地方自定义。

  

### 路线图
#### 基础功能开发
√ 基础UI设计  
√ 列出文件  
√ 列出磁盘驱动器  
√ 获取缩略图  
√ 多标签页  
√ 剪贴板  
√ 文件复制、粘贴、删除  
√ 文件列表交互  
√ 文件重命名  
√ 文件拖放  
√ 自由分屏  
√ 超级收藏夹  
→ 文件视图切换  
→ zip支持  
→ bitlocker支持  
→ OneDrive以及网络驱动器  

#### 进阶功能开发
→ 私密空间  
→ 文件夹标签（别名、颜色标记）  
→ fastcopy  
