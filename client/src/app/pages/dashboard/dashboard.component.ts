import { Component, signal, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { GroupService } from '../../services/group.service';
import { ThemeService } from '../../services/theme.service';
import { GroupDto, UserDto } from '../../models/interfaces';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private auth = inject(AuthService);
  private groupService = inject(GroupService);
  private theme = inject(ThemeService);
  private router = inject(Router);

  user = this.auth.user;
  isDark = this.theme.isDark;
  groups = signal<GroupDto[]>([]);
  isLoading = signal(true);
  showCreateModal = signal(false);
  newGroup = { name: '', description: '', defaultCurrency: 'INR' };

  ngOnInit() {
    this.loadGroups();
  }

  loadGroups() {
    this.isLoading.set(true);
    this.groupService.getMyGroups().subscribe({
      next: (g) => { this.groups.set(g); this.isLoading.set(false); },
      error: () => this.isLoading.set(false)
    });
  }

  createGroup() {
    this.groupService.createGroup(this.newGroup).subscribe({
      next: (g) => {
        this.groups.update(gs => [g, ...gs]);
        this.showCreateModal.set(false);
        this.newGroup = { name: '', description: '', defaultCurrency: 'INR' };
      }
    });
  }

  toggleTheme() { this.theme.toggle(); }
  logout() { this.auth.logout(); }

  getInitials(name: string): string {
    return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
  }

  getMemberCount(group: GroupDto): number { return group.members?.length || 0; }

  getGroupColor(index: number): string {
    const colors = ['#6C5CE7', '#00CEC9', '#FF6B6B', '#FDCB6E', '#74B9FF', '#A29BFE', '#55EFC4', '#E17055'];
    return colors[index % colors.length];
  }
}
