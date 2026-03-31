import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            refetchOnWindowFocus: false,
            retry: 1
        }
    }
});

function App() {
    return (
        <QueryClientProvider client={queryClient}>
            <div className="min-h-screen">
                <header className="bg-white shadow-sm p-4">
                    <h1 className="text-xl font-bold">ShortLinker Admin</h1>
                </header>
                <main className="p-6 max-w-7xl mx-auto">
                    {/* Content goes here */}
                    <p>Welcome to Tenant: {localStorage.getItem('tenant_id') || 'default'}</p>
                </main>
            </div>
        </QueryClientProvider>
    );
}

export default App;
