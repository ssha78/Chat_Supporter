using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ChatSupporter.Models;

public class Customer
{
    [JsonProperty("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;
    
    [JsonProperty("deviceModel")]
    public string DeviceModel { get; set; } = string.Empty;
    
    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;
    
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonProperty("phone")]
    public string Phone { get; set; } = string.Empty;
    
    [JsonProperty("purchaseDate")]
    public DateTime? PurchaseDate { get; set; }
    
    [JsonProperty("warrantyStatus")]
    public string WarrantyStatus { get; set; } = string.Empty;
    
    [JsonProperty("previousSessions")]
    public List<string> PreviousSessions { get; set; } = new();
    
    [JsonProperty("totalClaimsCount")]
    public int TotalClaimsCount { get; set; } = 0;
    
    [JsonProperty("lastContactDate")]
    public DateTime? LastContactDate { get; set; }
}