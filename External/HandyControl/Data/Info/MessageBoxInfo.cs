using HandyControl.Properties.Langs;
using System.Windows;
using System.Windows.Media;

namespace HandyControl.Data {
	public class MessageBoxInfo {
		public string Message { get; set; }

		public string Caption { get; set; }

		public MessageBoxButton Button { get; set; } = MessageBoxButton.OK;

		public MessageBoxImage Image { get; set; } = MessageBoxImage.None;

		public MessageBoxResult DefaultResult { get; set; } = MessageBoxResult.None;

		public Style Style { get; set; }

		public string StyleKey { get; set; }

		public string OkButtonText { get; set; } = Lang.Confirm;

		public string CancelButtonText { get; set; } = Lang.Cancel;

		public string YesButtonText { get; set; } = Lang.Yes;

		public string NoButtonText { get; set; } = Lang.No;

		/// <summary>
		/// 如果为null，则不显示
		/// </summary>
		public string CheckBoxText { get; set; }

		/// <summary>
		/// 通过这个获取是否Checked
		/// </summary>
		public bool IsChecked { get; set; }
	}
}
