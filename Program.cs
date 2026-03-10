using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Data.SqlClient;

#region Models

public class Product
{
    public int ProductId { get; private set; }
    public string ProductName { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }

    private int quantityAvailable;

    public int QuantityAvailable
    {
        get { return quantityAvailable; }
        private set
        {
            if (value < 0)
                throw new Exception("Quantity cannot be negative");
            quantityAvailable = value;
        }
    }

    public Product(int id, string name, string? category, decimal price, int qty)
    {
        ProductId = id;
        ProductName = name;
        Category = category;
        Price = price;
        QuantityAvailable = qty;
    }

    public void AddStock(int qty)
    {
        QuantityAvailable += qty;
    }

    public void ReduceStock(int qty)
    {
        if (qty > QuantityAvailable)
            throw new OutOfStockException("Not enough stock");

        QuantityAvailable -= qty;
    }
}

public class Order
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = "";
    public List<(int ProductId, int Quantity)> Items { get; set; } = new();
    public DateTime OrderDate { get; set; } = DateTime.Now;
}

#endregion

#region Exceptions

public class OutOfStockException : Exception
{
    public OutOfStockException(string message) : base(message) { }
}

#endregion

#region Repository

public class InventoryRepository
{
    public Dictionary<int, Product> inventory = new();

    public void AddProduct(Product p)
    {
        inventory[p.ProductId] = p;
    }

    public Product GetProduct(int id)
    {
        if (!inventory.ContainsKey(id))
            throw new Exception("Product not found");

        return inventory[id];
    }

    public IEnumerable<Product> GetAllProducts()
    {
        return inventory.Values;
    }

    public void SaveInventory(string path)
    {
        using StreamWriter writer = new StreamWriter(path);

        foreach (var p in inventory.Values)
        {
            writer.WriteLine($"{p.ProductId},{p.ProductName},{p.Category},{p.Price},{p.QuantityAvailable}");
        }
    }

    public void LoadInventory(string path)
    {
        if (!File.Exists(path))
            return;

        using StreamReader reader = new StreamReader(path);

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            var parts = line.Split(',');

            Product p = new Product(
                int.Parse(parts[0]),
                parts[1],
                parts[2],
                decimal.Parse(parts[3]),
                int.Parse(parts[4]));

            inventory[p.ProductId] = p;
        }
    }
}

#endregion

#region Services

public class OrderService
{
    Queue<Order> orderQueue = new();
    Stack<Order> shippedOrders = new();

    InventoryRepository repo;

    public OrderService(InventoryRepository repository)
    {
        repo = repository;
    }

    public void PlaceOrder(Order order)
    {
        orderQueue.Enqueue(order);
        Console.WriteLine("Order added to queue");
    }

    public async Task ProcessOrdersAsync()
    {
        while (orderQueue.Count > 0)
        {
            Order order = orderQueue.Dequeue();

            await Task.Run(() =>
            {
                decimal total = 0;

                foreach (var item in order.Items)
                {
                    Product p = repo.GetProduct(item.ProductId);
                    p.ReduceStock(item.Quantity);

                    total += p.Price * item.Quantity;
                }

                shippedOrders.Push(order);

                Console.WriteLine($"Order {order.OrderId} processed. Total = {total}");
            });
        }
    }

    public IEnumerable<Order> GetOrderHistory()
    {
        return shippedOrders;
    }

    public void SaveOrderToDatabase(Order order)
    {
        string connectionString = "your_connection_string";

        using SqlConnection conn = new SqlConnection(connectionString);

        conn.Open();

        SqlCommand cmd = new SqlCommand(
        "INSERT INTO Orders(OrderId,CustomerName,OrderDate) VALUES (@id,@name,@date)",
        conn);

        cmd.Parameters.AddWithValue("@id", order.OrderId);
        cmd.Parameters.AddWithValue("@name", order.CustomerName);
        cmd.Parameters.AddWithValue("@date", order.OrderDate);

        cmd.ExecuteNonQuery();
    }
}

#endregion

#region Program

class Program
{
    static InventoryRepository repo = new();
    static OrderService orderService = new(repo);

    static string inventoryFile = "inventory.txt";

    static async Task Main()
    {
        repo.LoadInventory(inventoryFile);

        while (true)
        {
            Console.WriteLine("\n1 Add Product");
            Console.WriteLine("2 View Inventory");
            Console.WriteLine("3 Place Order");
            Console.WriteLine("4 Process Orders");
            Console.WriteLine("5 View Low Stock");
            Console.WriteLine("6 Save Inventory");
            Console.WriteLine("7 Exit");

            Console.Write("Select option: ");
            int choice;
            int.TryParse(Console.ReadLine(), out choice);

            try
            {
                switch (choice)
                {
                    case 1:
                        AddProduct();
                        break;

                    case 2:
                        ViewInventory();
                        break;

                    case 3:
                        PlaceOrder();
                        break;

                    case 4:
                        await orderService.ProcessOrdersAsync();
                        break;

                    case 5:
                        ShowLowStock();
                        break;

                    case 6:
                        repo.SaveInventory(inventoryFile);
                        Console.WriteLine("Inventory saved.");
                        break;

                    case 7:
                        return;
                }
            }
            catch (OutOfStockException ex)
            {
                Console.WriteLine("Stock Error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                // optional cleanup
            }
        }
    }

    static void AddProduct()
    {
        Console.Write("Product Id: ");
        int id = int.Parse(Console.ReadLine());

        Console.Write("Name: ");
        string name = Console.ReadLine();

        Console.Write("Category: ");
        string category = Console.ReadLine();

        Console.Write("Price: ");
        decimal price = decimal.Parse(Console.ReadLine());

        Console.Write("Quantity: ");
        int qty = int.Parse(Console.ReadLine());

        Product p = new Product(id, name, category, price, qty);

        repo.AddProduct(p);

        Console.WriteLine("Product added.");
    }

    static void ViewInventory()
    {
        foreach (var p in repo.GetAllProducts())
        {
            Console.WriteLine($"{p.ProductId} | {p.ProductName} | {p.Category} | {p.Price} | Stock:{p.QuantityAvailable}");
        }
    }

    static void PlaceOrder()
    {
        Order order = new Order();

        Console.Write("Order Id: ");
        order.OrderId = int.Parse(Console.ReadLine());

        Console.Write("Customer Name: ");
        order.CustomerName = Console.ReadLine();

        while (true)
        {
            Console.Write("Product Id (0 to stop): ");
            int pid = int.Parse(Console.ReadLine());

            if (pid == 0)
                break;

            Console.Write("Quantity: ");
            int qty = int.Parse(Console.ReadLine());

            order.Items.Add((pid, qty));
        }

        orderService.PlaceOrder(order);
    }

    static void ShowLowStock()
    {
        var lowStock = repo.GetAllProducts()
                           .Where(p => p.QuantityAvailable < 10);

        foreach (var p in lowStock)
        {
            Console.WriteLine($"Low stock: {p.ProductName} ({p.QuantityAvailable})");
        }

        var expensive = repo.GetAllProducts()
                            .OrderByDescending(p => p.Price)
                            .FirstOrDefault();

        if (expensive != null)
            Console.WriteLine($"Most expensive: {expensive.ProductName} - {expensive.Price}");

        var sorted = repo.GetAllProducts().OrderBy(p => p.Price);

        Console.WriteLine("Products sorted by price:");
        foreach (var p in sorted)
            Console.WriteLine($"{p.ProductName} - {p.Price}");

        var categoryCount = repo.GetAllProducts()
                                .GroupBy(p => p.Category)
                                .Select(g => new { Category = g.Key, Count = g.Count() });

        foreach (var c in categoryCount)
            Console.WriteLine($"{c.Category} : {c.Count}");
    }
}

#endregion
