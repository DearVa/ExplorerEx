namespace ExplorerEx.Database.Shared;

public enum DbNavigateType {
	/// <summary>
	/// 默认值，说明不是映射
	/// </summary>
	NoNavigate,
	/// <summary>
	/// 一对一映射
	/// <remarks>
	///	<para>
	/// 用法1，小蝌蚪找妈妈
	///	<code>
	///	class Parent {
	///		public int Id { get; set; }
	///
	///		[DbColumn(nameof(Child.ParentId), DbNavigateType.OneToOne)]
	///		public DbProperty&lt;Child> Child { get; set; }
	///	}
	///
	///	class Child {
	///		public int Id { get; set; }
	///		public int ParentId { get; set; }
	///	} 
	///	</code>
	///	</para>
	///
	///	<para>
	///	用法2，妈妈找小蝌蚪
	///	<code>
	///	class Parent {
	///		public int Id { get; set; }
	///
	///		public int ChildId { get; set; }
	///
	///		[DbColumn(nameof(ChildId), DbNavigateType.OneToOne)]
	///		public DbProperty&lt;Child> Child { get; set; }
	///	}
	///
	///	class Child {
	///		public int Id { get; set; }
	///	} 
	///	</code>
	///	</para> 
	/// </remarks>
	/// 
	/// </summary>
	OneToOne,
	OneToMany,
	ManyToOne,
	ManyToMany,
}