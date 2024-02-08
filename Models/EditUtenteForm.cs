namespace final.Models;
public class EditUtenteForm{
    public EditUtenteForm(){}
    public EditUtenteForm(string Nome, string Cognome, string Comune, DateTime DataDiNascita)
    {
        this.Nome = Nome;
        this.Cognome = Cognome;
        this.Comune = Comune;
        this.DataDiNascita = DataDiNascita;
    }

    public string? Nome {get; set;} = null;
    public string? Cognome {get; set;} = null;
    public string? Comune {get; set;} = null;
    public DateTime? DataDiNascita {get; set;} = null;

}