using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OLearn.Data;
using OLearn.Models;
using OLearn.Services;

namespace OLearn.Pages.Admin;

public class UsersModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public UsersModel(ApplicationDbContext db) => _db = db;
    public List<User> Users { get; set; } = new();
    [TempData] public string? Message { get; set; }

    public async Task OnGetAsync() => Users = await _db.Users.OrderBy(u => u.Role).ThenBy(u => u.Username).ToListAsync();

    public async Task<IActionResult> OnPostAsync(string username, string displayName, string password)
    {
        username = username.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Username == username)) { Message = "That username is already taken."; return RedirectToPage(); }
        _db.Users.Add(new User { Username = username, DisplayName = displayName.Trim(), PasswordHash = PasswordService.Hash(password), Role = "Student" });
        await _db.SaveChangesAsync();
        Message = "Student created.";
        return RedirectToPage();
    }
}
