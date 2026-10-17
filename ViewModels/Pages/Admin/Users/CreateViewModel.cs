namespace ViewModels.Pages.Admin.Users;

public class CreateViewModel : CommonViewModel
{
	#region Constructor
	public CreateViewModel() : base()
	{
		Ordering = 10_000;
	}
	#endregion /Constructor

	#region Properties

	#region Password Property
	[System.ComponentModel.DataAnnotations.Display
		(Name = nameof(Resources.DataDictionary.Password),
		ResourceType = typeof(Resources.DataDictionary))]

	[System.ComponentModel.DataAnnotations.Required
		(AllowEmptyStrings = false,
		ErrorMessageResourceType = typeof(Resources.Messages.Validations),
		ErrorMessageResourceName = nameof(Resources.Messages.Validations.Required))]

		[System.ComponentModel.DataAnnotations.MinLength
			(length: Constants.MinLength.UserPassword,
			ErrorMessageResourceType = typeof(Resources.Messages.Validations),
			ErrorMessageResourceName = nameof(Resources.Messages.Validations.MinLength))]

		[System.ComponentModel.DataAnnotations.MaxLength
			(length: Constants.MaxLength.UserPassword,
			ErrorMessageResourceType = typeof(Resources.Messages.Validations),
			ErrorMessageResourceName = nameof(Resources.Messages.Validations.MaxLength))]

		[ViewModels.Validation.NotCommonPassword
			(ErrorMessageResourceType = typeof(Resources.Messages.Validations),
			ErrorMessageResourceName = nameof(Resources.Messages.Validations.CommonPassword))]

	//[System.ComponentModel.DataAnnotations.DataType
	//	(dataType: System.ComponentModel.DataAnnotations.DataType.Password)]
	public string? Password { get; set; }
	#endregion /Password Property

	#endregion /Properties
}
