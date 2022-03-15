# ExplorerEx

### ExplorerEx is a Swift, Multi-Tabbed, Modern UI and Humanized File Explorer.

![Preview](https://github.com/DearVa/ExplorerEx/blob/master/Images/preview.png)

[🇨🇳中文版](https://github.com/DearVa/ExplorerEx/blob/master/Readme_zh_CN.md)

Due to unknown reasons, the file manager (Explorer) of windows 11 is very slow, especially when opening folders, selecting files, copying and pasting. It can even stop responding for a few seconds on the low-end computer.

Some time ago, when I was working on Minecraft mod, I needed to switch frequently in multiple folders. Not only did it very slow, but also the "multi tab" function with high user voice has not been available yet. So I came up with the idea of developing a file manager myself.



### ExplorerEx has the following features:

* **Swift.** My standard is that a middle-end computer (such as i5 Series CPU or AMD CPU with the same performance) can open a folder within 500ms. Even a folder with a large number of files like C:\Windows\system32 (I just looked at it, there were 4778 files) still needs to be opened quickly.

* **Multi-Tabbed.** It supports dragging files onto tabs, copying, moving, creating shortcuts, etc. You can also drag a tab to split the screen, just like in Visual Studio Code.

  ![SplitScreen](https://github.com/DearVa/ExplorerEx/blob/master/Images/SplitScreen.png)

* **Fast preview.** You can hold Alt and point to a video, a music, a picture even a text file. Then a preview Window will show up, letting you to preview the content of it. You can scroll your mouse to fast forward and rewind.

  ![FastPreview0](https://github.com/DearVa/ExplorerEx/blob/master/Images/FastPreview0.png)

  ![FastPreview1](https://github.com/DearVa/ExplorerEx/blob/master/Images/FastPreview1.png)

* **Super bookmarks.** Windows built-in file manager not only cannot comment and add files, but also has only one "quick access" column, which cannot be classified. ExplorerEx can collect folders and even files directly! You can also add tags and categories to facilitate search, just like the Microsoft Edge or Google Chorme!

  ![Explorer can't](https://github.com/DearVa/ExplorerEx/blob/master/Images/ExplorerCantAddFile.jpg)

  ![ExplorerEx Can](https://github.com/DearVa/ExplorerEx/blob/master/Images/SuperBookmarks.png)

* **Modern UI**. I'm using HandyControl: [欢迎使用HandyControl | HandyOrg](https://handyorg.github.io/handycontrol/) to build my UI. I will modify it to make it tend to the fluent UI style of windows 11, and add some amazing animations (on the premise of fast response).

* **Humanized**. For ordinary users, the interface is simple and easy to understand. For users who are familiar with computers, there are also advanced functions such as creating symbol links and not prompting when modifying file extension names. Users can customize them in settings or other places.

  

### Road map
#### Basic function development

✓ Basic UI design  
✓ List files  
✓ List disk drives  
✓ Get thumbnails  
✓ Multi-tab  
✓ Clipboard  
✓ File copy, cut, paste, recycle and delete  
✓ File list interaction  
✓ File rename  
✓ File drag&drop  
✓ Free split screen  
✓ Super bookmarks  
→ Fast preview  
→ File view switching  
→ zip support  
→ bitlocker support  
→ OneDrive and network drives  

#### Advanced function development
→ Private space (File Locker)  
→ File label (Alias, Colorful tags)  
→ fastcopy (Multi-thread copy)   
