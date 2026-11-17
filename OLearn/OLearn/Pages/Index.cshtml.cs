using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OLearn.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => Redirect("/Dashboard");
}
