# Rohlik API (F#)

This is an F# implementation of the Rohlik.cz API client, converted from Python. It provides functionality to:

- Login to Rohlik.cz
- Logout from the service
- Retrieve order history
- Analyze products from order history

## Features

- **Login/Logout**: Authenticate with Rohlik.cz using email and password
- **Order History**: Retrieve delivered orders with product details
- **Product Analysis**: Get product summaries showing:
  - Product name, brand, and category
  - Total quantity ordered
  - Number of times ordered
  - Last order date
  - Average price

## Usage

### Command Line

```bash
# Show help for the Rohlik products command
dotnet run -- rohlik:products --help

# Analyze your order history
dotnet run -- rohlik:products credentials.json --order-limit 100
```

### Credentials File

Create a JSON file with your Rohlik credentials:

```json
{
  "username": "your-email@example.com",
  "password": "your-password"
}
```

### API Usage

```fsharp
open MF.Rohlik
open MF.ErrorHandling

let credentials = {
    Username = "your-email@example.com"
    Password = "your-password"
}

// Get product summary from last 50 orders
let result = Api.getOrderHistoryProductSummary credentials 50
```

## Architecture

The API is built using:

- **FSharp.Data**: For HTTP requests and JSON type providers
- **MF.ErrorHandling**: For AsyncResult error handling patterns
- **Cookie Management**: For session handling

## Implementation Notes

- Follows the same patterns as other APIs in the project (DoIt, etc.)
- Uses AsyncResult for async error handling
- Implements proper session management with login/logout
- Provides structured error handling with meaningful error messages
- Uses JSON type providers for type-safe API responses

## Error Handling

The API properly handles:
- Invalid credentials (401 errors)
- Network errors
- JSON parsing errors
- File not found errors for credentials

All errors are propagated through the AsyncResult pattern for consistent error handling.
