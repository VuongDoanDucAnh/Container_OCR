namespace DoAnOlympics.Api.DTOs;

public record RegisterDto(string MaTaiXe, string HoTen, string Username, string Password);
public record LoginDto(string Username, string Password);
