namespace Fuocherello.Models;
public class EditUserForm{
    public EditUserForm(){}
    public EditUserForm(string Name, string Surname, string City, DateTime DateOfBirth)
    {
        this.Name = Name;
        this.Surname = Surname;
        this.City = City;
        this.DateOfBirth = DateOfBirth;
    }

    public string? Name {get; set;} = null;
    public string? Surname {get; set;} = null;
    public string? City {get; set;} = null;
    public DateTime? DateOfBirth {get; set;} = null;

}