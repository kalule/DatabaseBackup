namespace DatabaseBackup.Dto
{
    public class BackupResponseDto
    {
        public string Message { get; set; } = null!;
        public List<BackupResultDto> Backups { get; set; } = new();
    }
}
