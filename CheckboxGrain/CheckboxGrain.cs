namespace CheckboxGrain;

using GrainInterfaces;

public class CheckboxGrain : Grain, ICheckboxGrain
{
    #region Public Methods and Operators

    public async Task<string> Hello()
    {
        return "World";
    }

    #endregion
}
