namespace CheckboxGrain.Models;

public class CheckboxState
{
    #region Public Properties

    public GrainId? CallbackGrain { get; set; }

    public byte[]? Checkboxes { get; set; }

    #endregion
}
