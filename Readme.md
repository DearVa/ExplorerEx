# ExplorerEx

### ExplorerEx is a Swift, Multi-Tabbed, Modern UI and Humanized File Explorer.

![Preview](https://raw.githubusercontent.com/DearVa/ExplorerEx/master/Images/preview.png)

[๐จ๐ณไธญๆ็](https://github.com/DearVa/ExplorerEx/blob/master/Readme_zh_CN.md)

Due to unknown reasons, the file manager (Explorer) of windows 11 is very slow, especially when opening folders, selecting files, copying and pasting. It even stop responding for a few seconds sometimes.

Some time ago, when I was working on Minecraft mod, I needed to switch frequently in multiple folders. Not only did it very slow, but also the "multi tab" function with high user voice has not been available yet. So I came up with the idea of developing a file manager myself.

## Stable > Swift > Good appearance

### ๐๐ I'm waiting for a new Icon. If you are good at design, please submit it to me!
### ๐๐ ๅพๆ ้ฟๆๆๅไธญโฆโฆๅฆๆๆจๆ้ฟ่ฎพ่ฎกๆ่ๆๅฅฝ็ๅๆ๏ผๆฌข่ฟๆไบคๆจ็ไฝๅ๏ผ


### Features:

* **Swift.** My standard is that a middle-end computer (such as i5 Series CPU or AMD CPU with the same performance) can open a folder within 500ms. Even a folder with a large number of files like C:\Windows\system32 (I just looked at it, there were 4778 files) still needs to be opened quickly.

* **Multi-Tabbed.** It supports dragging files onto tabs, copying, moving, creating shortcuts, etc. You can also drag a tab to split the screen, just like in Visual Studio Code.

  ![SplitScreen](https://github.com/DearVa/ExplorerEx/blob/master/Images/SplitScreen.png)

* **Fast preview.** You can hold Alt and point to a video, a music, a picture even a text file. Then a preview Window will show up, letting you to preview the content of it. You can scroll your mouse to fast forward and rewind.

  ![FastPreview0](https://github.com/DearVa/ExplorerEx/blob/master/Images/FastPreview0.png)

  ![FastPreview1](https://github.com/DearVa/ExplorerEx/blob/master/Images/FastPreview1.png)

* **Super bookmarks.** Windows built-in file manager not only cannot comment and add files, but also has only one "quick access" column, which cannot be classified. ExplorerEx can collect folders and even files directly! You can also add tags and categories to facilitate search, just like the Microsoft Edge or Google Chorme!

  ![Explorer can't](https://github.com/DearVa/ExplorerEx/blob/master/Images/ExplorerCantAddFile.jpg)

  ![ExplorerEx Can](https://github.com/DearVa/ExplorerEx/blob/master/Images/SuperBookmarks.png)

* **Modern UI**. I'm using HandyControl: [ๆฌข่ฟไฝฟ็จHandyControl | HandyOrg](https://handyorg.github.io/handycontrol/) to build my UI. I will modify it to make it tend to the fluent UI style of windows 11, and add some amazing animations (on the premise of fast response).

* **Humanized**. For ordinary users, the interface is simple and easy to understand. For users who are familiar with computers, there are also advanced functions such as creating symbol links and not prompting when modifying file extension names. Users can customize them in settings or other places.

  

### Road map
#### Basic function development

โ Basic UI design  
โ List files  
โ List disk drives  
โ Get thumbnails  
โ Multi-tab  
โ Clipboard  
โ File copy, cut, paste, recycle and delete  
โ File list interaction  
โ File rename  
โ File drag&drop  
โ Free split screen  
โ Super bookmarks  
โ Fast preview  
โ File view switching  
โ zip support  
โ bitlocker support  
โ OneDrive and network drives  

#### Advanced function development
โ Private space (File Locker)  
โ File label (Alias, Colorful tags)  
โ fastcopy (Multi-thread copy)   


### Special Thanks to
* [HandyControl](https://github.com/HandyOrg/HandyControl)
* [pinvoke.net](https://www.pinvoke.net/)
* [SvgToXaml](https://github.com/BerndK/SvgToXaml)
* [Meziantou.Framework](https://github.com/meziantou/Meziantou.Framework)
