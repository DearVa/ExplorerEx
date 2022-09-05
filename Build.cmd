dotnet publish ExplorerEx -c Release -r win-x64
C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe .\ExplorerProxy\ExplorerProxy.csproj /property:Configuration=Release,Platform=x64
copy .\ExplorerProxy\bin\x64\Release\ExplorerProxy.dll .\ExplorerEx\bin\Release\net6.0-windows\win-x64\publish
copy .\ExplorerProxy\bin\x64\Release\Interop.SHDocVw.dll .\ExplorerEx\bin\Release\net6.0-windows\win-x64\publish