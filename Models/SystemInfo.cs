using System.Text.Json.Serialization;

namespace SystemPerformanceNotifierService.Models;

public class SystemInfo
{
  [JsonPropertyName("ts")]
  public long Timestamp { get; set; }

  [JsonPropertyName("cpu")]
  public CpuInfo Cpu { get; set; } = new();

  [JsonPropertyName("gpu")]
  public GpuInfo Gpu { get; set; } = new();

  [JsonPropertyName("mem")]
  public MemoryInfo Memory { get; set; } = new();
}

public class CpuInfo
{
  [JsonPropertyName("usage")]
  public int Usage { get; set; } // Percentage as integer to save space

  [JsonPropertyName("temp")]
  public int Temperature { get; set; } // Celsius as integer

  [JsonPropertyName("fan")]
  public int FanSpeed { get; set; } // RPM as integer

  [JsonPropertyName("name")]
  public string Name { get; set; } = "";
}

public class GpuInfo
{
  [JsonPropertyName("usage")]
  public int Usage { get; set; }

  [JsonPropertyName("temp")]
  public int Temperature { get; set; }

  [JsonPropertyName("name")]
  public string Name { get; set; } = "";

  [JsonPropertyName("mem_used")]
  public int MemoryUsed { get; set; } // MB as integer

  [JsonPropertyName("mem_total")]
  public int MemoryTotal { get; set; }
}

public class MemoryInfo
{
  [JsonPropertyName("usage")]
  public int Usage { get; set; } // Percentage

  [JsonPropertyName("used")]
  public float Used { get; set; } // GB

  [JsonPropertyName("total")]
  public float Total { get; set; }

  [JsonPropertyName("avail")]
  public float Available { get; set; }
}