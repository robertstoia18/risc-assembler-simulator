namespace RiscEmulator.Logic;

/// <summary>
/// Politica de inlocuire folosita cand un set de cache este plin si apare un miss.
/// </summary>
public enum ReplacementPolicy
{
    /// <summary>Alege victima aleator dintre blocurile setului.</summary>
    Random,

    /// <summary>LRU exact: victima este blocul cu cel mai vechi timestamp de acces (contor global).</summary>
    LruExact,

    /// <summary>LRU aproximativ (NRU / Second-Chance / clock): foloseste un bit de referinta per bloc.</summary>
    LruApproximate
}
