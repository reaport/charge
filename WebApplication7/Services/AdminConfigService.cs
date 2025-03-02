using ChargeModule.Models;

namespace ChargeModule.Services
{
    public interface IAdminConfigService
    {
        AdminConfig GetConfig();
        void UpdateConfig(AdminConfig config);
    }

    public class AdminConfigService : IAdminConfigService
    {
        // Храним конфигурацию в памяти (можно расширить до БД или файла)
        private AdminConfig _config = new AdminConfig();

        public AdminConfig GetConfig() => _config;

        public void UpdateConfig(AdminConfig config)
        {
            // Обновляем только нужные параметры
            _config.MovementSpeed = config.MovementSpeed;
            _config.ConflictRetryCount = config.ConflictRetryCount;
        }
    }
}
