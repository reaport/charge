namespace ChargeModule.Models
{
    // Конфигурация, доступная через админку
    public class AdminConfig
    {
        // Скорость движения (единиц в секунду)
        public double MovementSpeed { get; set; } = 20.0;
        // Количество попыток при конфликте перемещения
        public int ConflictRetryCount { get; set; } = 15;
    }
}
