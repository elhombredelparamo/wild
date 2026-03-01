using Godot;
using System;
using System.IO;

namespace Wild;

/// <summary>
/// Sistema de logging simple que escribe en archivo latest.log
/// </summary>
public static class Logger
{
    private static readonly string LogPath = "latest.log";
    
    static Logger()
    {
        // Limpiar el archivo al iniciar
        ClearLog();
    }
    
    public static void ClearLog()
    {
        try
        {
            File.WriteAllText(LogPath, $"=== Log iniciado: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al limpiar log: {ex.Message}");
        }
    }
    
    public static void Log(string message)
    {
        // Siempre mostrar todos los logs
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logMessage = $"[{timestamp}] {message}";
        
        // Escribir en consola
        GD.Print(logMessage);
        
        // Escribir en archivo
        try
        {
            File.AppendAllText("latest.log", logMessage + "\n");
        }
        catch (System.Exception ex)
        {
            GD.Print($"Error escribiendo log: {ex.Message}");
        }
    }
    
    public static void LogError(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] ERROR: {message}\n";
            File.AppendAllText(LogPath, logEntry);
            
            // También mostrar en consola de Godot
            GD.PrintErr(message);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error al escribir error en log: {ex.Message}");
        }
    }
    
    public static void LogWarning(string message)
    {
        // Redirigir a Log para mantener consistencia
        Log($"WARNING: {message}");
    }
}
