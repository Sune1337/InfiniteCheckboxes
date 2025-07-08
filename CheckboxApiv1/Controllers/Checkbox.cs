namespace CheckBoxApiv1.Controllers;

using GrainInterfaces;

using Microsoft.AspNetCore.Mvc;

[Route("api/v1/[controller]/[action]/{id?}")]
[ApiController]
public class Checkbox
{
    #region Fields

    private readonly IGrainFactory _grainFactory;

    #endregion

    #region Constructors and Destructors

    public Checkbox(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    #endregion

    #region Public Methods and Operators

    [HttpGet]
    public async Task<string> Get()
    {
        var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>("test");
        return await checkboxGrain.Hello();
    }

    #endregion
}
