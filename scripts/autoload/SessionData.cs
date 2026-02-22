using Godot;

namespace Wild;

/// <summary>
/// Datos de la partida actual (nueva o cargada). Rellenado desde el menú "Nueva partida"
/// y leído por GameWorld u otros sistemas. Ver contexto/menus-y-servidor.txt.
/// </summary>
public partial class SessionData : Node
{
    /// <summary>Semilla para la generación procedural del terreno.</summary>
    public long WorldSeed { get; set; }

    /// <summary>Nombre del personaje del jugador.</summary>
    public string CharacterName { get; set; } = "";

    /// <summary>Género: "Hombre" o "Mujer".</summary>
    public string Gender { get; set; } = "Hombre";

    /// <summary>Nombre del mundo/partida.</summary>
    public string WorldName { get; set; } = "";

    /// <summary>True si el género elegido es Hombre.</summary>
    public bool IsMale => Gender == "Hombre";

    /// <summary>Genera una semilla aleatoria (para botón "Aleatorio").</summary>
    public static long RandomSeed()
    {
        return GD.Randi();
    }
}
