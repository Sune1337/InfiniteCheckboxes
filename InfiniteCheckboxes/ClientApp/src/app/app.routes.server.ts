import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
    { path: 'Checkboxes/:id', renderMode: RenderMode.Client },
    { path: 'War/:id', renderMode: RenderMode.Client },
    { path: 'Minesweeper/:id', renderMode: RenderMode.Client },
    {
        path: '**',
        renderMode: RenderMode.Prerender
    }
];
