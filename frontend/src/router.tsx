import { createBrowserRouter } from 'react-router'
import { HomeRedirect, RequireRole } from './auth/RequireRole'
import { AppLayout } from './layout/AppLayout'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { RouteErrorPage } from './pages/RouteErrorPage'
import { paths } from './routes'
import { studentRoutes } from './studentRoutes'
import { technicianRoutes } from './technicianRoutes'

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      {
        // Pathless so that a render error is shown inside the layout, not instead of it.
        errorElement: <RouteErrorPage />,
        children: [
          { index: true, element: <HomeRedirect /> },
          { path: paths.login, element: <LoginPage /> },
          { element: <RequireRole role="Student" />, children: studentRoutes },
          { element: <RequireRole role="Technician" />, children: technicianRoutes },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
