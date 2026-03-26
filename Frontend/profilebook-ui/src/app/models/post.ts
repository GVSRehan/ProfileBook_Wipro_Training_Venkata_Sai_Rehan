export interface PostComment {
  postCommentId: number;
  postId: number;
  userId: number;
  username: string;
  commentText: string;
  createdAt: string;
}

export interface Post {
  postId: number;
  content: string;
  userId: number;
  username: string;
  profileImage?: string | null;
  postImage: string | null;
  status: string;
  createdAt: string;
  reviewedAt?: string | null;
  likeCount: number;
  commentCount: number;
  shareCount: number;
  likedByCurrentUser: boolean;
  comments: PostComment[];
}
