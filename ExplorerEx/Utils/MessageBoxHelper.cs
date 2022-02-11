using HandyControl.Data;
using System.Windows;

namespace ExplorerEx.Utils; 

internal static class MessageBoxHelper {
	/// <summary>
	/// 弹出一个带有默认操作的对话框（下次不再提示）
	/// 如果用户已经指定了默认操作，就直接返回true
	/// </summary>
	/// <param name="msg"></param>
	/// <param name="caption"></param>
	/// <param name="configKey"></param>
	/// <returns></returns>
	public static bool AskWithDefault(string configKey, string msg, string caption = null) {
		if (ConfigHelper.LoadBoolean(configKey)) {
			return true;
		}
		var msi = new MessageBoxInfo {
			CheckBoxText = "Dont_show_this_message_again".L(),
			Message = msg,
			Caption = caption ?? "Tip".L(),
			Image = MessageBoxImage.Question,
			Button = MessageBoxButton.YesNo,
			IsChecked = false
		};
		var result = HandyControl.Controls.MessageBox.Show(msi);
		if (result == MessageBoxResult.Yes) {
			if (msi.IsChecked) {
				ConfigHelper.Save(configKey, true);
			}
			return true;
		}
		return false;
	}
}