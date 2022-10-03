using ExplorerEx.Model;

namespace ExplorerExTest;

public class FileListViewItemsTests {
	[SetUp]
	public void Setup() {
		ExplorerEx.App.Main();
	}

	[Test]
	public void TestFolderOnlyItem() {
		var item = new FolderOnlyItem(new DriveInfo("c"));
		Assert.Multiple(() => {
			Assert.That(item.IsFolder, Is.True);
			Assert.That(item.IsReadonly, Is.False);
			Assert.That(item.IsVirtual, Is.False);
			Assert.That(item.FullPath, Is.EqualTo(@"C:\"));
		});
	}
}