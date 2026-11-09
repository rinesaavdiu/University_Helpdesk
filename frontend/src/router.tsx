import { createBrowserRouter, Navigate } from 'react-router'
import { AppLayout } from './layout/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RouteErrorPage } from './pages/RouteErrorPage'
import { TechnicianTicketsPage } from './pages/TechnicianTicketsPage'
import { paths } from './routes'
import { studentRoutes } from './studentRoutes'

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      {
        // Pathless so that a render error is shown inside the layout, not instead of it.
        errorElement: <RouteErrorPage />,
        children: [
          { index: true, element: <Navigate to={paths.login} replace /> },
          { path: paths.login, element: <LoginPage /> },
          ...studentRoutes,
          { path: paths.technicianTickets, element: <TechnicianTicketsPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
